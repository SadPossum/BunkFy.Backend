namespace BunkFy.Modules.Guests.Contracts;

public sealed record GuestAnonymisationReceiptDto(
    int ContractVersion,
    Guid ReceiptId,
    Guid IdempotencyKey,
    Guid RoutingPropertyId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid GuestId,
    long SelectedGuestVersion,
    long ResultingGuestVersion,
    GuestAnonymisationDisposition Disposition,
    GuestAnonymisationReason Reason,
    int AffectedPropertyCount,
    string ApprovalEvidenceSha256,
    string PolicySetSha256,
    Guid EventId,
    string ActorId,
    DateTimeOffset CompletedAtUtc,
    string CanonicalSha256);

public enum GuestAnonymisationDisposition
{
    Unknown = 0,
    Completed = 1
}

public enum GuestAnonymisationReason
{
    Unknown = 0,
    ProfileAnonymised = 1
}
