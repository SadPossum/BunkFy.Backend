namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationListResponse(
    IReadOnlyCollection<ReservationDto> Reservations,
    int Page,
    int PageSize);
