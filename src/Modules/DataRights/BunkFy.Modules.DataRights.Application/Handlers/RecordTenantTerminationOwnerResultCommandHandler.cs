namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class RecordTenantTerminationOwnerResultCommandHandler(
    ITenantTerminationRepository repository,
    ITenantTerminationReplayStore replayStore,
    TenantTerminationPhasePlanner planner,
    ITenantTerminationCoordinationSignal coordinationSignal)
    : ICommandHandler<
        RecordTenantTerminationOwnerResultCommand,
        TenantTerminationOwnerResultRecorded>
{
    public async Task<Result<TenantTerminationOwnerResultRecorded>> HandleAsync(
        RecordTenantTerminationOwnerResultCommand command,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await repository.GetProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<TenantTerminationOwnerResultRecorded>(
                DataRightsApplicationErrors.TenantTerminationProcessNotFound);
        }

        if (command.WorkItemId == Guid.Empty ||
            command.OperationRevision != process.OperationRevision ||
            command.TaskRunId == Guid.Empty ||
            command.TaskAttempt <= 0 ||
            command.ExpectedWorkItemVersion <= 0 ||
            process.Status != TenantTerminationProcessStatus.Running ||
            !TenantTerminationPhasePlanner.TryMapPhase(
                process.Phase,
                out TenantTerminationContributionPhase phase,
                out TenantTerminationOwnerPhase ownerPhase) ||
            command.Phase != phase)
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
            return Result.Failure<TenantTerminationOwnerResultRecorded>(
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
        if (planned is null)
        {
            return Invalid();
        }

        TenantTerminationOwnerWorkItem workItem =
            validated.Value.WorkByOwner[planned.OwnerKey];
        if (workItem.LastAttemptAtUtc is not DateTimeOffset startedAtUtc)
        {
            return Invalid();
        }

        TenantTerminationContributionRequest request = CreateRequest(
            process,
            workItem,
            planned,
            phase,
            startedAtUtc);
        TenantTerminationReplayAttemptCoordinate coordinate = new(
            process.ScopeId,
            process.Id,
            workItem.Id,
            command.TaskRunId,
            command.TaskAttempt);
        TenantTerminationReplayAttempt? replay =
            await replayStore.ReadAttemptAsync(
                coordinate,
                cancellationToken).ConfigureAwait(false);
        if (replay?.Result is null ||
            replay.Dispatch.RecordedAtUtc < startedAtUtc ||
            !TenantTerminationReplayProof.MatchesDispatch(
                replay.Dispatch,
                request,
                planned.OwnerKey,
                planned.CatalogVersion,
                planned.CatalogSha256,
                planned.ExecutionBoundary,
                command.TaskRunId,
                command.TaskAttempt) ||
            !replay.Result.HasValidProof(replay.Dispatch))
        {
            return Invalid();
        }

        TenantTerminationContributionResult contribution =
            replay.Result.Contribution;
        long previousWorkItemVersion = workItem.Version;
        Result recorded = workItem.RecordResult(
            Map(contribution.Status),
            contribution.ResultCode,
            contribution.AffectedCount,
            contribution.RetainedMinimumCount,
            contribution.RemainingActiveCount,
            contribution.HoldReviewAtUtc,
            contribution.SelectedProofRevision,
            contribution.ResultingProofRevision,
            contribution.CatalogVersion,
            contribution.CatalogSha256,
            command.TaskRunId,
            command.TaskAttempt,
            command.ExpectedWorkItemVersion,
            contribution.RecordedAtUtc);
        if (recorded.IsFailure)
        {
            return Result.Failure<TenantTerminationOwnerResultRecorded>(
                recorded.Error);
        }

        if (workItem.Version != previousWorkItemVersion)
        {
            _ = await coordinationSignal.EnqueueAsync(
                process,
                workItem.ResultRecordedAtUtc.GetValueOrDefault(),
                cancellationToken).ConfigureAwait(false);
        }

        Result<IReadOnlyList<TenantTerminationPlannedDispatch>> ready =
            planner.FindReadyDispatches(process, workItems);
        return ready.IsSuccess
            ? Result.Success(new TenantTerminationOwnerResultRecorded(
                workItem.State,
                workItem.Version,
                ready.Value))
            : Result.Failure<TenantTerminationOwnerResultRecorded>(
                ready.Error);
    }

    private static TenantTerminationContributionRequest CreateRequest(
        TenantTerminationProcess process,
        TenantTerminationOwnerWorkItem workItem,
        TenantTerminationPlannedOwnerWork planned,
        TenantTerminationContributionPhase phase,
        DateTimeOffset startedAtUtc)
    {
        return new(
            planned.OwnerContractVersion,
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            process.OperationRevision,
            process.TerminationEpoch,
            phase,
            workItem.Id,
            workItem.IdempotencyKey,
            process.PolicyEvidenceSha256,
            TenantTerminationCoordination.ExecutorActorId,
            startedAtUtc.Add(
                BeginTenantTerminationOwnerWorkCommandHandler
                    .OwnerCallTimeout));
    }

    private static TenantTerminationOwnerWorkState Map(
        TenantTerminationContributionStatus status) =>
        status switch
        {
            TenantTerminationContributionStatus.Completed =>
                TenantTerminationOwnerWorkState.Completed,
            TenantTerminationContributionStatus.Blocked =>
                TenantTerminationOwnerWorkState.Blocked,
            TenantTerminationContributionStatus.RetryRequired =>
                TenantTerminationOwnerWorkState.RetryRequired,
            TenantTerminationContributionStatus.Failed =>
                TenantTerminationOwnerWorkState.Failed,
            _ => TenantTerminationOwnerWorkState.Unknown
        };

    private static Result<TenantTerminationOwnerResultRecorded> Invalid() =>
        Result.Failure<TenantTerminationOwnerResultRecorded>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}
