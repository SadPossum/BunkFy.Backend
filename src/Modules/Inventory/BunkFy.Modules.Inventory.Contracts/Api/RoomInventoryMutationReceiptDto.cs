namespace BunkFy.Modules.Inventory.Contracts;

public sealed record RoomInventoryMutationReceiptDto(
    Guid PropertyId,
    Guid RoomId,
    InventorySalesMode SalesMode,
    long Version);
