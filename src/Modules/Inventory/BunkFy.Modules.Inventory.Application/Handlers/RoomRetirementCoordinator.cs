namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RoomRetirementCoordinator(
    IRoomRetirementRepository retirements,
    IInventoryAvailabilityRepository availability,
    ISystemClock clock,
    IIdGenerator idGenerator)
{
    public async Task<RoomRetirementDto> TryAdvanceAsync(
        RoomRetirementProcess process,
        Guid? excludedAllocationId,
        IReadOnlyCollection<Guid> excludedBlockIds,
        CancellationToken cancellationToken)
    {
        RoomInventoryImpactSnapshot impact = await this.GetImpactAsync(
            process,
            excludedAllocationId,
            excludedBlockIds,
            cancellationToken).ConfigureAwait(false);
        if (process.State == InventoryRetirementProcessState.Draining &&
            !impact.PreventsRoomRetirementFinalization)
        {
            Result requested = process.RequestFinalization(idGenerator.NewId(), clock.UtcNow);
            if (requested.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Validated room-retirement advancement failed with '{requested.Error.Code}'.");
            }
        }

        return process.ToDto(impact);
    }

    public async Task TryAdvanceForUnitsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        Guid? excludedAllocationId,
        IReadOnlyCollection<Guid> excludedBlockIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<RoomRetirementProcess> processes = await retirements
            .ListActiveForUnitsAsync(propertyId, inventoryUnitIds, cancellationToken)
            .ConfigureAwait(false);
        foreach (RoomRetirementProcess process in processes.Where(item => item.State == InventoryRetirementProcessState.Draining))
        {
            await this.TryAdvanceAsync(
                process,
                excludedAllocationId,
                excludedBlockIds,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<RoomRetirementDto> GetDtoAsync(
        RoomRetirementProcess process,
        CancellationToken cancellationToken) =>
        process.ToDto(await this.GetImpactAsync(
            process,
            excludedAllocationId: null,
            excludedBlockIds: [],
            cancellationToken).ConfigureAwait(false));

    private async Task<RoomInventoryImpactSnapshot> GetImpactAsync(
        RoomRetirementProcess process,
        Guid? excludedAllocationId,
        IReadOnlyCollection<Guid> excludedBlockIds,
        CancellationToken cancellationToken) =>
        await availability.GetRoomImpactAsync(
            process.PropertyId,
            process.RoomId,
            excludedAllocationId,
            excludedBlockIds,
            cancellationToken).ConfigureAwait(false) ??
        throw new InvalidOperationException("A room-retirement process references missing Inventory topology.");
}
