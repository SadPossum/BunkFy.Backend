namespace BunkFy.Modules.Guests.Contracts;

public sealed record GuestListResponse(
    IReadOnlyCollection<GuestListItemDto> Guests,
    int Page,
    int PageSize,
    bool HasMore);
