namespace BunkFy.Modules.Inventory.Contracts;

public sealed record RoomInventoryListResponse(
    IReadOnlyCollection<RoomInventoryDto> Rooms,
    int Page,
    int PageSize);
