namespace BunkFy.Modules.Inventory.Contracts;

public sealed record InventoryUnitDto(
    Guid InventoryUnitId,
    Guid PropertyId,
    Guid RoomId,
    Guid? BedId,
    InventoryUnitKind Kind,
    string Label,
    bool IsSellable,
    bool IsTopologyActive);
