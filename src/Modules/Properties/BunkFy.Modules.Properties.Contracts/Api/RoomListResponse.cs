namespace BunkFy.Modules.Properties.Contracts;

public sealed record RoomListResponse(
    IReadOnlyCollection<RoomListItemDto> Rooms,
    int Page,
    int PageSize,
    bool HasMore);
