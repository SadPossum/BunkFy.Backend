namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationAnonymisationReceiptDto(
    int ContractVersion,
    Guid ReceiptId,
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid ReservationId,
    long SelectedReservationVersion,
    long ResultingReservationVersion,
    long SelectedDetailsRevision,
    long ResultingDetailsRevision,
    ReservationAnonymisationDisposition Disposition,
    ReservationAnonymisationReason Reason,
    int RedactedHistoryCount,
    int RemovedGuestLinkCount,
    int ReducedExternalOperationCount,
    int SuppressedReminderCount,
    string ApprovalEvidenceSha256,
    string PolicyEvidenceSha256,
    Guid EventId,
    string ActorId,
    DateTimeOffset CompletedAtUtc,
    string CanonicalSha256);

public enum ReservationAnonymisationDisposition
{
    Unknown = 0,
    Completed = 1
}

public enum ReservationAnonymisationReason
{
    Unknown = 0,
    ReservationOwnerDataRedacted = 1
}
