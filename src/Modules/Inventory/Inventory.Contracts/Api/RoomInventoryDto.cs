namespace Inventory.Contracts;

public sealed record RoomInventoryDto(
    Guid PropertyId,
    Guid RoomId,
    string RoomName,
    InventorySalesMode SalesMode,
    long Version,
    IReadOnlyCollection<InventoryUnitDto> Units);
