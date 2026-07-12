namespace BunkFy.Modules.Guests.Contracts;

public sealed record GuestListResponse(
    IReadOnlyCollection<GuestProfileDto> Guests,
    int Page,
    int PageSize);
