namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationListItemDto(
    Guid ReservationId,
    Guid PropertyId,
    DateOnly Arrival,
    DateOnly Departure,
    TimeOnly? ExpectedArrivalTime,
    TimeOnly? ExpectedDepartureTime,
    string PrimaryGuestName,
    int GuestCount,
    int InventoryUnitCount,
    ReservationSourceKind SourceKind,
    ReservationStatus Status);
