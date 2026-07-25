namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationDataRightsCorrectionReceiptDto(
    int ContractVersion,
    Guid ReceiptId,
    Guid CaseId,
    long ApprovalRevision,
    Guid ReservationId,
    long PreviousVersion,
    long CurrentVersion,
    long PreviousDetailsRevision,
    long CurrentDetailsRevision,
    IReadOnlyCollection<string> ChangedFields,
    Guid DetailsChangeEventId,
    Guid EventId,
    DateTimeOffset CompletedAtUtc);
