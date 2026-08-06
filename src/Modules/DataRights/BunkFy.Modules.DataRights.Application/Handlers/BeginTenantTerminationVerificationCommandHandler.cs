namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class BeginTenantTerminationVerificationCommandHandler(
    TenantTerminationMutationCoordinator mutations,
    ISystemClock clock)
    : ICommandHandler<
        BeginTenantTerminationVerificationCommand,
        TenantTerminationVerificationPhaseStart>
{
    public async Task<Result<TenantTerminationVerificationPhaseStart>>
        HandleAsync(
            BeginTenantTerminationVerificationCommand command,
            CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await mutations.AcquireProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<TenantTerminationVerificationPhaseStart>(
                DataRightsApplicationErrors.TenantTerminationProcessNotFound);
        }

        if (command.ExpectedProcessVersion <= 0 ||
            command.ExpectedProcessVersion == long.MaxValue ||
            process.Phase != TenantTerminationProcessPhase.Verify ||
            process.DestroyCompletedOperationRevision is not long
                destroyOperationRevision)
        {
            return Invalid();
        }

        if (process.Status == TenantTerminationProcessStatus.Pending)
        {
            Result began = process.BeginPhase(
                TenantTerminationProcessPhase.Verify,
                command.ExpectedProcessVersion,
                command.ActorId,
                clock.UtcNow);
            if (began.IsFailure)
            {
                return Result.Failure<
                    TenantTerminationVerificationPhaseStart>(began.Error);
            }
        }
        else if (process.Status != TenantTerminationProcessStatus.Running ||
            process.Version != command.ExpectedProcessVersion + 1 ||
            !string.Equals(
                process.LastChangedBy,
                command.ActorId?.Trim(),
                StringComparison.Ordinal))
        {
            return Invalid();
        }

        if (destroyOperationRevision <= process.ApprovalRevision ||
            destroyOperationRevision >= process.OperationRevision)
        {
            return Invalid();
        }

        return Result.Success(new TenantTerminationVerificationPhaseStart(
            process.Id,
            process.Version,
            destroyOperationRevision,
            process.OperationRevision,
            TenantTerminationExecutionIdentity.CreateVerificationTaskRunId(
                process.Id,
                process.OperationRevision)));
    }

    private static Result<TenantTerminationVerificationPhaseStart> Invalid() =>
        Result.Failure<TenantTerminationVerificationPhaseStart>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}
