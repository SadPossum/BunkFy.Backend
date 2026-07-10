namespace Reservations.Contracts;

public sealed record ReservationListResponse(
    IReadOnlyCollection<ReservationDto> Reservations,
    int Page,
    int PageSize);
