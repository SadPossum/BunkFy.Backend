namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationDetailsSnapshotDto(
    DateOnly Arrival,
    DateOnly Departure,
    TimeOnly? ExpectedArrivalTime,
    TimeOnly? ExpectedDepartureTime,
    IReadOnlyCollection<Guid> InventoryUnitIds,
    string PrimaryGuestName,
    string? Email,
    string? Phone,
    int GuestCount,
    string? Notes);
