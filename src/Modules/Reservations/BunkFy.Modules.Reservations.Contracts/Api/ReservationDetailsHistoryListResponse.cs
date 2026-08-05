namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationDetailsHistoryListResponse(
    IReadOnlyCollection<ReservationDetailsHistoryItem> Items,
    int Page,
    int PageSize,
    bool HasMore);
