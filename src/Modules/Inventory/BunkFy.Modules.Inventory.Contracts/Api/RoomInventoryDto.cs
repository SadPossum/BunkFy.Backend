namespace BunkFy.Modules.Inventory.Contracts;

public sealed record RoomInventoryDto(
    Guid PropertyId,
    Guid RoomId,
    string RoomName,
    string? BuildingLabel,
    string? FloorLabel,
    InventorySalesMode SalesMode,
    long Version,
    IReadOnlyCollection<InventoryUnitDto> Units);
