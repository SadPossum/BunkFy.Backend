namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffAnonymisationReceiptDto(
    int ContractVersion,
    Guid ReceiptId,
    Guid IdempotencyKey,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid StaffMemberId,
    long SelectedStaffVersion,
    long ResultingStaffVersion,
    long SelectedOperationLockRevision,
    long ResultingOperationLockRevision,
    StaffAnonymisationDisposition Disposition,
    StaffAnonymisationReason Reason,
    string ApprovalEvidenceSha256,
    string StateBindingsSha256,
    Guid EventId,
    string ActorId,
    DateTimeOffset CompletedAtUtc,
    string CanonicalSha256);

public enum StaffAnonymisationDisposition
{
    Unknown = 0,
    Completed = 1
}

public enum StaffAnonymisationReason
{
    Unknown = 0,
    ProfileAnonymised = 1
}
