namespace BunkFy.Modules.Inventory.Contracts;

public sealed record InventoryBlockTarget(
    InventoryBlockTargetKind Kind,
    string? BuildingLabel = null,
    string? FloorLabel = null,
    Guid? RoomId = null,
    Guid? InventoryUnitId = null);
