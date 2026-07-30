namespace BunkFy.Modules.Workspaces.Contracts;

public sealed record WorkspaceStaffCorrelationAnonymisationReceiptDto(
    int ContractVersion,
    Guid ReceiptId,
    Guid IdempotencyKey,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid AnchorProcessId,
    Guid StaffMemberId,
    long SelectedStaffVersion,
    long SelectedAnchorVersion,
    long ResultingAnchorVersion,
    int OnboardingRecordsScrubbed,
    int AccessProcessRecordsScrubbed,
    int AccessPlanRecordsScrubbed,
    WorkspaceStaffCorrelationAnonymisationDisposition Disposition,
    WorkspaceStaffCorrelationAnonymisationReason Reason,
    string ApprovalEvidenceSha256,
    string StateBindingSha256,
    string ResultingStateSha256,
    string ActorId,
    DateTimeOffset CompletedAtUtc,
    string CanonicalSha256);

public enum WorkspaceStaffCorrelationAnonymisationDisposition
{
    Unknown = 0,
    Completed = 1
}

public enum WorkspaceStaffCorrelationAnonymisationReason
{
    Unknown = 0,
    SubjectCorrelationsPseudonymised = 1
}
