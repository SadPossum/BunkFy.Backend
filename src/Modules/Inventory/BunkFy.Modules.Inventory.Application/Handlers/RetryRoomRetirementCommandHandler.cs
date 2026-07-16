namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RetryRoomRetirementCommandHandler(
    IRoomRetirementRepository retirements,
    IInventoryAvailabilityRepository availability,
    RoomRetirementCoordinator coordinator,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<RetryRoomRetirementCommand, RoomRetirementDto>
{
    public async Task<Result<RoomRetirementDto>> HandleAsync(
        RetryRoomRetirementCommand command,
        CancellationToken cancellationToken)
    {
        RoomRetirementProcess? process = await retirements
            .GetAsync(command.PropertyId, command.TopologyChangeId, cancellationToken)
            .ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomRetirementNotFound);
        }

        if (process.State != InventoryRetirementProcessState.Rejected)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomRetirementRetryInvalid);
        }

        RoomInventoryImpactSnapshot? impact = await availability.GetRoomImpactAsync(
            process.PropertyId,
            process.RoomId,
            excludedAllocationId: null,
            excludedBlockIds: [],
            cancellationToken).ConfigureAwait(false);
        if (impact is null)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomNotFound);
        }

        if (impact.PreventsRoomRetirementFinalization)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomRetirementStillDraining);
        }

        Result requested = process.RequestFinalization(idGenerator.NewId(), clock.UtcNow);
        return requested.IsFailure
            ? Result.Failure<RoomRetirementDto>(requested.Error)
            : Result.Success(await coordinator.GetDtoAsync(process, cancellationToken).ConfigureAwait(false));
    }
}
