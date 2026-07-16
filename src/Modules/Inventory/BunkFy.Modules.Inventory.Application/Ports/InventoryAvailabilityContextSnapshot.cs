namespace BunkFy.Modules.Inventory.Application.Ports;

public sealed record InventoryAvailabilityContextSnapshot(
    IReadOnlyCollection<InventoryAllocationUnitSnapshot> Units,
    IReadOnlyCollection<Guid> ConflictUnitIds);

public sealed record InventoryAvailabilityConflictSnapshot(
    bool HasManualBlockConflict,
    bool HasActiveAllocationConflict);
