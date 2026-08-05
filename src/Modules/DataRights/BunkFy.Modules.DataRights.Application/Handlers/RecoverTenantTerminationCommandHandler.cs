namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Tasks;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class RecoverTenantTerminationCommandHandler(
    TenantTerminationStartCoordinator startCoordinator,
    ITenantTerminationCaseRepository cases,
    ITenantTerminationRepository processes,
    ITenantTerminationCoordinationSignal coordinationSignal,
    ITenantTerminationTaskScheduler taskScheduler,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<RecoverTenantTerminationCommand,
        TenantTerminationStartDto>
{
    public async Task<Result<TenantTerminationStartDto>> HandleAsync(
        RecoverTenantTerminationCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<TenantTerminationStartDto>(
                DataRightsApplicationErrors.TenantRequired);
        }

        TenantTerminationProcess? process = await processes.GetProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        StartTenantTerminationCommand start = new(
            command.CaseId,
            command.ProcessId,
            command.ApprovalEvidence,
            command.ExpectedCaseVersion,
            command.ActorId);
        if (process is null)
        {
            return command.ExpectedProcessVersion.HasValue
                ? Result.Failure<TenantTerminationStartDto>(
                    DataRightsApplicationErrors
                        .TenantTerminationStartConflict)
                : await startCoordinator.RecoverAsync(
                    start,
                    cancellationToken).ConfigureAwait(false);
        }

        DataRightsCase? dataRightsCase = await cases.GetAsync(
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<TenantTerminationStartDto>(
                DataRightsApplicationErrors.TenantTerminationCaseNotFound);
        }

        if (dataRightsCase.Version != command.ExpectedCaseVersion ||
            process.Version != command.ExpectedProcessVersion)
        {
            return Result.Failure<TenantTerminationStartDto>(
                DataRightsApplicationErrors.VersionConflict);
        }

        Result<TenantTerminationStartDto> exact =
            await startCoordinator.RecoverAsync(
                start,
                cancellationToken).ConfigureAwait(false);
        if (exact.IsFailure ||
            process.Status is
                TenantTerminationProcessStatus.Completed or
                TenantTerminationProcessStatus.Cancelled)
        {
            return exact;
        }

        if (process.Status is
            TenantTerminationProcessStatus.Blocked or
            TenantTerminationProcessStatus.Failed)
        {
            return Result.Failure<TenantTerminationStartDto>(
                DataRightsApplicationErrors
                    .TenantTerminationRecoveryRequiresRetry);
        }

        if (process.Phase == TenantTerminationProcessPhase.Verify &&
            process.Status == TenantTerminationProcessStatus.Running)
        {
            await taskScheduler.EnqueueVerificationAsync(
                scopeContext.ScopeId,
                process.Id,
                process.OperationRevision,
                cancellationToken).ConfigureAwait(false);
            return exact;
        }

        if (!await coordinationSignal.EnqueueAsync(
                process,
                clock.UtcNow,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TenantTerminationStartDto>(
                DataRightsApplicationErrors
                    .TenantTerminationExecutionStateInvalid);
        }

        return exact;
    }
}
