namespace BunkFy.Modules.Guests.Contracts;

public sealed record GuestStayHistoryListResponse(
    IReadOnlyCollection<GuestStayHistoryItem> Stays,
    int Page,
    int PageSize,
    bool HasMore);
