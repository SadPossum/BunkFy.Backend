namespace BunkFy.Modules.Guests.Contracts;

public sealed record GuestDataRightsCorrectionReceiptDto(
    Guid ReceiptId,
    Guid CaseId,
    long ApprovalRevision,
    Guid GuestId,
    long PreviousVersion,
    long CurrentVersion,
    IReadOnlyCollection<string> ChangedFields,
    Guid EventId,
    DateTimeOffset CompletedAtUtc);
