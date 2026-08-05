namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class BeginTenantTerminationOwnerWorkCommandHandler(
    ITenantTerminationRepository repository,
    TenantTerminationPhasePlanner planner,
    ITenantTerminationReplayStore replayStore,
    ISystemClock clock)
    : ICommandHandler<
        BeginTenantTerminationOwnerWorkCommand,
        TenantTerminationOwnerWorkStart>
{
    internal static readonly TimeSpan OwnerCallTimeout =
        TimeSpan.FromMinutes(2);

    public async Task<Result<TenantTerminationOwnerWorkStart>> HandleAsync(
        BeginTenantTerminationOwnerWorkCommand command,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await repository.GetProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<TenantTerminationOwnerWorkStart>(
                DataRightsApplicationErrors.TenantTerminationProcessNotFound);
        }

        if (command.WorkItemId == Guid.Empty ||
            command.OperationRevision <= process.ApprovalRevision ||
            command.OperationRevision > process.OperationRevision ||
            command.TaskRunId == Guid.Empty ||
            command.TaskAttempt is <= 0 or >
                TenantTerminationCoordination.MaximumTaskAttempts ||
            command.ExecutionBoundary is not (
                TenantTerminationExecutionBoundary.TenantScopedTask or
                TenantTerminationExecutionBoundary.GlobalControlTask) ||
            !TryMapOwnerPhase(command.Phase, out TenantTerminationOwnerPhase ownerPhase))
        {
            return Invalid();
        }

        string ownerKey = command.OwnerKey?.Trim() ?? string.Empty;
        TenantTerminationOwnerWorkItem? selectedWorkItem =
            await repository.GetOwnerWorkItemAsync(
                process.Id,
                ownerPhase,
                ownerKey,
                command.OperationRevision,
                cancellationToken).ConfigureAwait(false);
        if (selectedWorkItem is null ||
            !MatchesStableCoordinates(
                process,
                selectedWorkItem,
                command,
                ownerPhase,
                ownerKey))
        {
            return Invalid();
        }

        if (IsTerminalReplay(selectedWorkItem, command))
        {
            return Result.Success(CreateTerminalReplay(
                selectedWorkItem,
                command.ExecutionBoundary));
        }

        bool currentExecution = IsCurrentExecution(process, command);
        if ((!currentExecution ||
             selectedWorkItem.TaskRunId != command.TaskRunId) &&
            await this.HasHistoricalResultAsync(
                process,
                selectedWorkItem,
                command,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(CreateTerminalReplay(
                selectedWorkItem,
                command.ExecutionBoundary));
        }

        if (!currentExecution)
        {
            return Invalid();
        }

        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems =
            await repository.ListOwnerWorkItemsAsync(
                process.Id,
                ownerPhase,
                process.OperationRevision,
                cancellationToken).ConfigureAwait(false);
        Result<TenantTerminationValidatedPhase> validated =
            planner.ValidateWorkItems(process, workItems);
        if (validated.IsFailure)
        {
            return Result.Failure<TenantTerminationOwnerWorkStart>(
                validated.Error);
        }

        TenantTerminationPlannedOwnerWork? planned = validated.Value
            .PlannedWork
            .SingleOrDefault(candidate =>
                candidate.WorkItemId == command.WorkItemId &&
                string.Equals(
                    candidate.OwnerKey,
                    command.OwnerKey?.Trim(),
                    StringComparison.Ordinal));
        if (planned is null ||
            planned.ExecutionBoundary != command.ExecutionBoundary ||
            planned.DependsOnOwnerKeys.Any(dependency =>
                validated.Value.WorkByOwner[dependency].State !=
                    TenantTerminationOwnerWorkState.Completed))
        {
            return Invalid();
        }

        TenantTerminationOwnerWorkItem workItem =
            validated.Value.WorkByOwner[planned.OwnerKey];
        Guid expectedTaskRunId = workItem.State switch
        {
            TenantTerminationOwnerWorkState.Prepared or
                TenantTerminationOwnerWorkState.RetryRequired =>
                TenantTerminationExecutionIdentity.CreateTaskRunId(
                    workItem.Id,
                    workItem.AttemptCount + 1),
            TenantTerminationOwnerWorkState.Processing =>
                workItem.TaskRunId ?? Guid.Empty,
            _ => Guid.Empty
        };
        if (expectedTaskRunId != command.TaskRunId)
        {
            return Invalid();
        }

        Result began = workItem.BeginProcessing(
            command.TaskRunId,
            command.TaskAttempt,
            workItem.Version,
            clock.UtcNow);
        if (began.IsFailure || workItem.LastAttemptAtUtc is not { } startedAtUtc)
        {
            return began.IsFailure
                ? Result.Failure<TenantTerminationOwnerWorkStart>(began.Error)
                : Invalid();
        }

        TenantTerminationContributionRequest request = new(
            planned.OwnerContractVersion,
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            process.OperationRevision,
            process.TerminationEpoch,
            command.Phase,
            workItem.Id,
            workItem.IdempotencyKey,
            process.PolicyEvidenceSha256,
            TenantTerminationCoordination.ExecutorActorId,
            startedAtUtc.Add(OwnerCallTimeout));
        return Result.Success(new TenantTerminationOwnerWorkStart(
            DispatchRequired: true,
            workItem.State,
            workItem.Version,
            planned.OwnerKey,
            planned.CatalogVersion,
            planned.CatalogSha256,
            planned.ExecutionBoundary,
            request,
            ReadyDispatches: []));
    }

    private static bool IsTerminalReplay(
        TenantTerminationOwnerWorkItem workItem,
        BeginTenantTerminationOwnerWorkCommand command) =>
        (workItem.State is
            TenantTerminationOwnerWorkState.Completed or
            TenantTerminationOwnerWorkState.Blocked or
            TenantTerminationOwnerWorkState.RetryRequired or
            TenantTerminationOwnerWorkState.Failed) &&
        workItem.TaskRunId == command.TaskRunId &&
        command.TaskAttempt >= workItem.LastTaskAttempt;

    private static TenantTerminationOwnerWorkStart CreateTerminalReplay(
        TenantTerminationOwnerWorkItem workItem,
        TenantTerminationExecutionBoundary executionBoundary) =>
        new(
            DispatchRequired: false,
            workItem.State,
            workItem.Version,
            workItem.OwnerKey,
            workItem.CatalogVersion,
            workItem.CatalogSha256,
            executionBoundary,
            Request: null,
            ReadyDispatches: []);

    private async Task<bool> HasHistoricalResultAsync(
        TenantTerminationProcess process,
        TenantTerminationOwnerWorkItem workItem,
        BeginTenantTerminationOwnerWorkCommand command,
        CancellationToken cancellationToken)
    {
        for (int attempt = command.TaskAttempt; attempt >= 1; attempt--)
        {
            TenantTerminationReplayAttempt? replay =
                await replayStore.ReadAttemptAsync(
                    new(
                        process.ScopeId,
                        process.Id,
                        workItem.Id,
                        command.TaskRunId,
                        attempt),
                    cancellationToken).ConfigureAwait(false);
            if (replay?.Result is not null &&
                MatchesHistoricalResult(
                    process,
                    workItem,
                    command,
                    replay))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesHistoricalResult(
        TenantTerminationProcess process,
        TenantTerminationOwnerWorkItem workItem,
        BeginTenantTerminationOwnerWorkCommand command,
        TenantTerminationReplayAttempt replay)
    {
        TenantTerminationReplayDispatch dispatch = replay.Dispatch;
        return dispatch.HasValidProof() &&
            replay.Result!.HasValidProof(dispatch) &&
            string.Equals(
                dispatch.Coordinate.TenantId,
                process.ScopeId,
                StringComparison.Ordinal) &&
            dispatch.Coordinate.ProcessId == process.Id &&
            dispatch.Coordinate.WorkItemId == workItem.Id &&
            dispatch.Coordinate.TaskRunId == command.TaskRunId &&
            dispatch.Coordinate.TaskAttempt <= command.TaskAttempt &&
            dispatch.CaseId == process.CaseId &&
            dispatch.ApprovalRevision == process.ApprovalRevision &&
            dispatch.OperationRevision == command.OperationRevision &&
            dispatch.TerminationEpoch == process.TerminationEpoch &&
            dispatch.Phase == command.Phase &&
            dispatch.IdempotencyKey == workItem.IdempotencyKey &&
            string.Equals(
                dispatch.PolicyEvidenceSha256,
                process.PolicyEvidenceSha256,
                StringComparison.Ordinal) &&
            string.Equals(
                dispatch.OwnerKey,
                workItem.OwnerKey,
                StringComparison.Ordinal) &&
            dispatch.OwnerContractVersion == workItem.OwnerContractVersion &&
            dispatch.CatalogVersion == workItem.CatalogVersion &&
            string.Equals(
                dispatch.CatalogSha256,
                workItem.CatalogSha256,
                StringComparison.Ordinal) &&
            dispatch.ExecutionBoundary == command.ExecutionBoundary &&
            string.Equals(
                dispatch.ExecutingActorId,
                TenantTerminationCoordination.ExecutorActorId,
                StringComparison.Ordinal);
    }

    private static bool MatchesStableCoordinates(
        TenantTerminationProcess process,
        TenantTerminationOwnerWorkItem workItem,
        BeginTenantTerminationOwnerWorkCommand command,
        TenantTerminationOwnerPhase ownerPhase,
        string ownerKey) =>
        ownerKey.Length > 0 &&
        workItem.Id == command.WorkItemId &&
        string.Equals(
            workItem.ScopeId,
            process.ScopeId,
            StringComparison.Ordinal) &&
        workItem.ProcessId == process.Id &&
        workItem.CaseId == process.CaseId &&
        workItem.ApprovalRevision == process.ApprovalRevision &&
        workItem.OperationRevision == command.OperationRevision &&
        workItem.TerminationEpoch == process.TerminationEpoch &&
        workItem.Phase == ownerPhase &&
        string.Equals(workItem.OwnerKey, ownerKey, StringComparison.Ordinal) &&
        workItem.Id == TenantTerminationExecutionIdentity.CreateWorkItemId(
            process.Id,
            command.OperationRevision,
            ownerPhase,
            ownerKey) &&
        workItem.IdempotencyKey ==
            TenantTerminationExecutionIdentity.CreateWorkItemIdempotencyKey(
                process.Id,
                command.OperationRevision,
                ownerPhase,
                ownerKey) &&
        string.Equals(
            workItem.PolicyEvidenceSha256,
            process.PolicyEvidenceSha256,
            StringComparison.Ordinal);

    private static bool IsCurrentExecution(
        TenantTerminationProcess process,
        BeginTenantTerminationOwnerWorkCommand command) =>
        process.Status == TenantTerminationProcessStatus.Running &&
        process.OperationRevision == command.OperationRevision &&
        TenantTerminationPhasePlanner.TryMapPhase(
            process.Phase,
            out TenantTerminationContributionPhase phase,
            out _) &&
        phase == command.Phase;

    private static bool TryMapOwnerPhase(
        TenantTerminationContributionPhase phase,
        out TenantTerminationOwnerPhase ownerPhase)
    {
        ownerPhase = phase switch
        {
            TenantTerminationContributionPhase.Freeze =>
                TenantTerminationOwnerPhase.Freeze,
            TenantTerminationContributionPhase.Export =>
                TenantTerminationOwnerPhase.Export,
            TenantTerminationContributionPhase.Destroy =>
                TenantTerminationOwnerPhase.Destroy,
            TenantTerminationContributionPhase.Restore =>
                TenantTerminationOwnerPhase.Restore,
            _ => TenantTerminationOwnerPhase.Unknown
        };
        return ownerPhase != TenantTerminationOwnerPhase.Unknown;
    }

    private static Result<TenantTerminationOwnerWorkStart> Invalid() =>
        Result.Failure<TenantTerminationOwnerWorkStart>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}
