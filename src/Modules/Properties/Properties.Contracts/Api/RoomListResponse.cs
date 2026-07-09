namespace Properties.Contracts;

public sealed record RoomListResponse(
    IReadOnlyCollection<RoomDto> Rooms,
    int Page,
    int PageSize);
