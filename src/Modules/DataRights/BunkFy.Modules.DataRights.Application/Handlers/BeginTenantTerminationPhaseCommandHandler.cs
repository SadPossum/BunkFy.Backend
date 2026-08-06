namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class BeginTenantTerminationPhaseCommandHandler(
    ITenantTerminationRepository repository,
    TenantTerminationMutationCoordinator mutations,
    TenantTerminationPhasePlanner planner,
    ITenantTerminationCoordinationSignal coordinationSignal,
    ISystemClock clock)
    : ICommandHandler<
        BeginTenantTerminationPhaseCommand,
        TenantTerminationPhaseStart>
{
    public async Task<Result<TenantTerminationPhaseStart>> HandleAsync(
        BeginTenantTerminationPhaseCommand command,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await mutations.AcquireProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<TenantTerminationPhaseStart>(
                DataRightsApplicationErrors.TenantTerminationProcessNotFound);
        }

        if (command.ExpectedProcessVersion <= 0 ||
            command.ExpectedProcessVersion == long.MaxValue ||
            process.Phase != command.Phase ||
            !TenantTerminationPhasePlanner.TryMapPhase(
                command.Phase,
                out _,
                out TenantTerminationOwnerPhase ownerPhase))
        {
            return Invalid();
        }

        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems;
        bool beganPhase = process.Status == TenantTerminationProcessStatus.Pending;
        if (beganPhase)
        {
            DateTimeOffset nowUtc = clock.UtcNow;
            Result began = process.BeginPhase(
                command.Phase,
                command.ExpectedProcessVersion,
                command.ActorId,
                nowUtc);
            if (began.IsFailure)
            {
                return Result.Failure<TenantTerminationPhaseStart>(began.Error);
            }

            Result<IReadOnlyList<TenantTerminationOwnerWorkItem>> prepared =
                planner.PrepareWorkItems(process, nowUtc);
            if (prepared.IsFailure)
            {
                return Result.Failure<TenantTerminationPhaseStart>(
                    prepared.Error);
            }

            workItems = prepared.Value;
            foreach (TenantTerminationOwnerWorkItem workItem in workItems)
            {
                await repository.AddOwnerWorkItemAsync(
                    workItem,
                    cancellationToken).ConfigureAwait(false);
            }

            _ = await coordinationSignal.EnqueueAsync(
                process,
                process.LastChangedAtUtc,
                cancellationToken).ConfigureAwait(false);
        }
        else if (process.Status == TenantTerminationProcessStatus.Running &&
            process.Version == command.ExpectedProcessVersion + 1 &&
            string.Equals(
                process.LastChangedBy,
                command.ActorId?.Trim(),
                StringComparison.Ordinal))
        {
            workItems = await repository.ListOwnerWorkItemsAsync(
                process.Id,
                ownerPhase,
                process.OperationRevision,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return Invalid();
        }

        Result<IReadOnlyList<TenantTerminationPlannedDispatch>> ready =
            planner.FindReadyDispatches(process, workItems);
        return ready.IsSuccess
            ? Result.Success(new TenantTerminationPhaseStart(
                process.Id,
                process.Phase,
                process.Version,
                process.OperationRevision,
                ready.Value))
            : Result.Failure<TenantTerminationPhaseStart>(ready.Error);
    }

    private static Result<TenantTerminationPhaseStart> Invalid() =>
        Result.Failure<TenantTerminationPhaseStart>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}
