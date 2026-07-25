namespace BunkFy.Modules.Reservations.Contracts;

public enum ReservationProcessingRestrictionActionDto
{
    Unknown = 0,
    Apply = 1,
    Release = 2
}

public sealed record ReservationProcessingRestrictionReceiptDto(
    Guid ReceiptId,
    Guid RestrictionId,
    ReservationProcessingRestrictionActionDto Action,
    Guid PropertyId,
    Guid ReservationId,
    Guid CaseId,
    long ApprovalRevision,
    long SelectedReservationVersion,
    int ContractVersion,
    long RestrictionVersion,
    long ProjectionRevision,
    bool EffectiveRestricted,
    Guid EventId,
    DateTimeOffset CompletedAtUtc);
