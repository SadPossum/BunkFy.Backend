namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationMutationReceiptDto(
    Guid ReservationId,
    Guid PropertyId,
    ReservationStatus Status,
    long DetailsRevision,
    long Version);
