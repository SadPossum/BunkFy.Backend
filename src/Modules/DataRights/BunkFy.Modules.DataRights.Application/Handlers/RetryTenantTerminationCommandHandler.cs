namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class RetryTenantTerminationCommandHandler(
    ITenantTerminationRepository repository,
    TenantTerminationMutationCoordinator mutations,
    TenantTerminationPhasePlanner planner,
    ITenantTerminationCoordinationSignal coordinationSignal,
    ISystemClock clock)
    : ICommandHandler<RetryTenantTerminationCommand,
        TenantTerminationProcessDto>
{
    public async Task<Result<TenantTerminationProcessDto>> HandleAsync(
        RetryTenantTerminationCommand command,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await mutations.AcquireProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<TenantTerminationProcessDto>(
                DataRightsApplicationErrors
                    .TenantTerminationProcessNotFound);
        }

        if (process.Version != command.ExpectedProcessVersion ||
            process.Status is not TenantTerminationProcessStatus.Blocked and
                not TenantTerminationProcessStatus.Failed ||
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
        Result<TenantTerminationValidatedPhase> validated =
            planner.ValidateRecoverableWorkItems(process, workItems);
        if (validated.IsFailure)
        {
            return Result.Failure<TenantTerminationProcessDto>(
                validated.Error);
        }

        TenantTerminationOwnerWorkItem[] retryable = validated.Value
            .WorkByOwner.Values
            .Where(item => item.State is
                TenantTerminationOwnerWorkState.Blocked or
                TenantTerminationOwnerWorkState.Failed)
            .OrderBy(item => item.OwnerKey, StringComparer.Ordinal)
            .ToArray();
        if (retryable.Length == 0)
        {
            return Invalid();
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        foreach (TenantTerminationOwnerWorkItem workItem in retryable)
        {
            Result requeued = workItem.Requeue(workItem.Version, nowUtc);
            if (requeued.IsFailure)
            {
                return Result.Failure<TenantTerminationProcessDto>(
                    requeued.Error);
            }
        }

        Result resumed = process.Requeue(
            command.ExpectedProcessVersion,
            command.ActorId,
            nowUtc);
        if (resumed.IsFailure)
        {
            return Result.Failure<TenantTerminationProcessDto>(
                resumed.Error);
        }

        if (!await coordinationSignal.EnqueueAsync(
                process,
                nowUtc,
                cancellationToken).ConfigureAwait(false))
        {
            return Invalid();
        }

        return Result.Success(process.ToDto());
    }

    private static Result<TenantTerminationProcessDto> Invalid() =>
        Result.Failure<TenantTerminationProcessDto>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}
