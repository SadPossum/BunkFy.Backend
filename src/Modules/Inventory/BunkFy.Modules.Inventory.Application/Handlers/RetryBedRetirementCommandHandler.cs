namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RetryBedRetirementCommandHandler(
    IBedRetirementRepository retirements,
    IInventoryAvailabilityRepository availability,
    BedRetirementCoordinator coordinator,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<RetryBedRetirementCommand, BedRetirementDto>
{
    public async Task<Result<BedRetirementDto>> HandleAsync(
        RetryBedRetirementCommand command,
        CancellationToken cancellationToken)
    {
        BedRetirementProcess? process = await retirements
            .GetAsync(command.PropertyId, command.TopologyChangeId, cancellationToken)
            .ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<BedRetirementDto>(InventoryApplicationErrors.BedRetirementNotFound);
        }

        if (process.State != InventoryRetirementProcessState.Rejected)
        {
            return Result.Failure<BedRetirementDto>(InventoryApplicationErrors.BedRetirementRetryInvalid);
        }

        BedRetirementImpactSnapshot? impact = await availability.GetBedRetirementImpactAsync(
            process.PropertyId,
            process.RoomId,
            process.BedId,
            excludedAllocationId: null,
            excludedBlockIds: [],
            cancellationToken).ConfigureAwait(false);
        if (impact is null)
        {
            return Result.Failure<BedRetirementDto>(InventoryApplicationErrors.InventoryUnitNotFound);
        }

        if (impact.HasActiveClaims)
        {
            return Result.Failure<BedRetirementDto>(InventoryApplicationErrors.BedRetirementStillDraining);
        }

        Result requested = process.RequestFinalization(idGenerator.NewId(), clock.UtcNow);
        return requested.IsFailure
            ? Result.Failure<BedRetirementDto>(requested.Error)
            : Result.Success(await coordinator.GetDtoAsync(process, cancellationToken).ConfigureAwait(false));
    }
}
