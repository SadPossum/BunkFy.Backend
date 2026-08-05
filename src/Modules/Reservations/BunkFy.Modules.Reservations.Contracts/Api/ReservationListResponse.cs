namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationListResponse(
    IReadOnlyCollection<ReservationListItemDto> Reservations,
    int Page,
    int PageSize,
    bool HasMore);
