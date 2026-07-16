namespace BunkFy.Modules.Inventory.Application.Handlers;

internal sealed class InventoryRetirementCoordinator(
    BedRetirementCoordinator beds,
    RoomRetirementCoordinator rooms)
{
    public async Task TryAdvanceForUnitsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        Guid? excludedAllocationId,
        IReadOnlyCollection<Guid> excludedBlockIds,
        CancellationToken cancellationToken)
    {
        await beds.TryAdvanceForUnitsAsync(
            propertyId,
            inventoryUnitIds,
            excludedAllocationId,
            excludedBlockIds,
            cancellationToken).ConfigureAwait(false);
        await rooms.TryAdvanceForUnitsAsync(
            propertyId,
            inventoryUnitIds,
            excludedAllocationId,
            excludedBlockIds,
            cancellationToken).ConfigureAwait(false);
    }
}
