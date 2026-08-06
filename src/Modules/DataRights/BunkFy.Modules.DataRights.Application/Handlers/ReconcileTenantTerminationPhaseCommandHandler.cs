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

internal sealed class ReconcileTenantTerminationPhaseCommandHandler(
    ITenantTerminationRepository repository,
    TenantTerminationMutationCoordinator mutations,
    TenantTerminationPhaseEvaluator evaluator,
    IEnumerable<ITenantTerminationContributor> contributors,
    ITenantTerminationCoordinationSignal coordinationSignal,
    ISystemClock clock)
    : ICommandHandler<
        ReconcileTenantTerminationPhaseCommand,
        TenantTerminationPhaseReconciliation>
{
    public async Task<Result<TenantTerminationPhaseReconciliation>> HandleAsync(
        ReconcileTenantTerminationPhaseCommand command,
        CancellationToken cancellationToken)
    {
        TenantTerminationMutationState? state =
            await mutations.AcquireProcessAndCaseAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        TenantTerminationProcess? process = state?.Process;
        if (process is null)
        {
            return Result.Failure<TenantTerminationPhaseReconciliation>(
                DataRightsApplicationErrors.TenantTerminationProcessNotFound);
        }

        if (command.ExpectedProcessVersion <= 0 ||
            command.ExpectedProcessVersion == long.MaxValue ||
            process.Version != command.ExpectedProcessVersion ||
            process.Phase != command.Phase ||
            process.Status != TenantTerminationProcessStatus.Running ||
            process.OperationRevision != command.OperationRevision ||
            !TenantTerminationPhasePlanner.TryMapPhase(
                process.Phase,
                out _,
                out TenantTerminationOwnerPhase ownerPhase))
        {
            return Invalid();
        }

        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems =
            await repository.ListOwnerWorkItemsAsync(
                process.Id,
                ownerPhase,
                process.OperationRevision,
                cancellationToken).ConfigureAwait(false);
        Result<TenantTerminationPhaseEvaluation> evaluated =
            evaluator.Evaluate(process, workItems);
        if (evaluated.IsFailure)
        {
            return Result.Failure<TenantTerminationPhaseReconciliation>(
                evaluated.Error);
        }

        long previousProcessVersion = process.Version;
        bool exportArtifactRequired =
            evaluated.Value.Disposition ==
                TenantTerminationPhaseDisposition.Completed &&
            process.Phase == TenantTerminationProcessPhase.Export &&
            !process.HasCurrentExportConfirmation();
        Result transition = exportArtifactRequired
            ? Result.Success()
            : evaluated.Value.Disposition switch
            {
                TenantTerminationPhaseDisposition.Running => Result.Success(),
                TenantTerminationPhaseDisposition.Blocked => process.RecordBlocked(
                    process.Phase,
                    process.OperationRevision,
                    evaluated.Value.OutcomeCode!,
                    evaluated.Value.HoldReviewAtUtc,
                    process.Version,
                    command.ActorId,
                    clock.UtcNow),
                TenantTerminationPhaseDisposition.Failed => process.RecordFailed(
                    process.Phase,
                    process.OperationRevision,
                    evaluated.Value.OutcomeCode!,
                    process.Version,
                    command.ActorId,
                    clock.UtcNow),
                TenantTerminationPhaseDisposition.Completed =>
                    this.CompletePhase(process, workItems, command),
                _ => Result.Failure(
                    DataRightsApplicationErrors
                        .TenantTerminationExecutionStateInvalid)
            };
        if (transition.IsFailure)
        {
            return Result.Failure<TenantTerminationPhaseReconciliation>(
                transition.Error);
        }

        if (process.Status == TenantTerminationProcessStatus.Cancelled)
        {
            DataRightsCase? dataRightsCase = state!.Case;
            if (dataRightsCase is null)
            {
                return Invalid();
            }

            Result caseCancelled = dataRightsCase
                .CompleteTenantTerminationCancellation(
                    process.ApprovalRevision,
                    process.PolicyEvidenceSha256,
                    dataRightsCase.Version,
                    command.ActorId,
                    process.LastChangedAtUtc);
            if (caseCancelled.IsFailure)
            {
                return Result.Failure<
                    TenantTerminationPhaseReconciliation>(
                        caseCancelled.Error);
            }
        }

        if (process.Version != previousProcessVersion)
        {
            _ = await coordinationSignal.EnqueueAsync(
                process,
                process.LastChangedAtUtc,
                cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<TenantTerminationPlannedDispatch> ready =
            evaluated.Value.Disposition ==
                TenantTerminationPhaseDisposition.Running
                ? evaluated.Value.ReadyDispatches
                : [];
        return Result.Success(new TenantTerminationPhaseReconciliation(
            process.Id,
            process.Phase,
            process.Status,
            process.Version,
            process.OperationRevision,
            ready,
            exportArtifactRequired));
    }

    private Result CompletePhase(
        TenantTerminationProcess process,
        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems,
        ReconcileTenantTerminationPhaseCommand command)
    {
        if (workItems.Count == 0 ||
            workItems.Any(item =>
                item.State != TenantTerminationOwnerWorkState.Completed ||
                !item.ResultRecordedAtUtc.HasValue))
        {
            return Result.Failure(
                DataRightsApplicationErrors
                    .TenantTerminationExecutionStateInvalid);
        }

        DateTimeOffset completedAtUtc = workItems.Max(item =>
            item.ResultRecordedAtUtc.GetValueOrDefault());

        return process.Phase switch
        {
            TenantTerminationProcessPhase.Freeze => this.CompleteFreeze(
                process,
                workItems,
                command,
                completedAtUtc),
            TenantTerminationProcessPhase.Restore =>
                process.CompleteCancellation(
                    process.OperationRevision,
                    process.Version,
                    command.ActorId,
                    completedAtUtc),
            TenantTerminationProcessPhase.Export or
                TenantTerminationProcessPhase.Destroy =>
                process.CompletePhase(
                    process.Phase,
                    process.OperationRevision,
                    process.Version,
                    command.ActorId,
                    completedAtUtc),
            _ => Result.Failure(
                DataRightsApplicationErrors
                    .TenantTerminationExecutionStateInvalid)
        };
    }

    private Result CompleteFreeze(
        TenantTerminationProcess process,
        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems,
        ReconcileTenantTerminationPhaseCommand command,
        DateTimeOffset frozenAtUtc)
    {
        long workspaceFenceRevision = workItems.Count == 1
            ? workItems[0].ResultingProofRevision.GetValueOrDefault()
            : 0;
        if (workspaceFenceRevision <= 0 ||
            workItems[0].State != TenantTerminationOwnerWorkState.Completed ||
            workItems[0].ResultRecordedAtUtc != frozenAtUtc)
        {
            return Result.Failure(
                DataRightsApplicationErrors
                    .TenantTerminationExecutionStateInvalid);
        }

        Result<IReadOnlyList<ITenantTerminationContributor>> ordered =
            TenantTerminationContributorSet.OrderForPhase(
                contributors,
                TenantTerminationContributionPhase.Export);
        if (ordered.IsFailure)
        {
            return Result.Failure(ordered.Error);
        }

        TenantTerminationExportOwnerCatalogEntry[] catalog = ordered.Value
            .Select(contributor => new TenantTerminationExportOwnerCatalogEntry(
                contributor.Descriptor.OwnerKey,
                contributor.Descriptor.ContractVersion,
                contributor.Descriptor.CatalogVersion,
                contributor.Descriptor.CatalogSha256))
            .ToArray();
        TenantTerminationFrozenRevision snapshot = new(
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            process.OperationRevision,
            process.TerminationEpoch,
            workspaceFenceRevision,
            process.PolicyEvidenceSha256,
            frozenAtUtc,
            catalog);
        string frozenRevisionSha256;
        try
        {
            frozenRevisionSha256 = TenantTerminationExportFragmentAssembler
                .ComputeFrozenRevisionSha256(snapshot);
        }
        catch (DataRightsExportGenerationException)
        {
            return Result.Failure(
                DataRightsApplicationErrors
                    .TenantTerminationExecutionStateInvalid);
        }

        return process.CompleteFreeze(
            process.OperationRevision,
            workspaceFenceRevision,
            frozenRevisionSha256,
            catalog.Select(owner =>
                new TenantTerminationFrozenOwnerDescriptor(
                    owner.OwnerKey,
                    owner.ContractVersion,
                    owner.CatalogVersion,
                    owner.CatalogSha256))
                .ToArray(),
            command.ExpectedProcessVersion,
            command.ActorId,
            frozenAtUtc);
    }

    private static Result<TenantTerminationPhaseReconciliation> Invalid() =>
        Result.Failure<TenantTerminationPhaseReconciliation>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}
