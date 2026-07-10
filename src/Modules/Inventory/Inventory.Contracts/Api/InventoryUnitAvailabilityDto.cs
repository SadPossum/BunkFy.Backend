namespace Inventory.Contracts;

public sealed record InventoryUnitAvailabilityDto(
    InventoryUnitDto Unit,
    bool IsAvailable,
    IReadOnlyCollection<Guid> ActiveBlockIds,
    IReadOnlyCollection<Guid> ActiveAllocationIds);
