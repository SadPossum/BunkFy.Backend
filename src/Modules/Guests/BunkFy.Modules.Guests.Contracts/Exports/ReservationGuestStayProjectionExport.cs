namespace BunkFy.Modules.Guests.Contracts;

public sealed record ReservationGuestStayProjectionExport(
    string TenantId,
    Guid GuestId,
    Guid ReservationId,
    Guid PropertyId,
    GuestStayRole Role,
    DateOnly Arrival,
    DateOnly Departure,
    GuestStayStatus Status,
    DateOnly? CheckedInBusinessDate,
    DateOnly? NoShowBusinessDate,
    DateOnly? CheckedOutBusinessDate,
    bool IsCurrentParticipant,
    long ReservationVersion);
