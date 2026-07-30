namespace BunkFy.Modules.Workspaces.Contracts;

public sealed record WorkspaceStaffOnboardingDataRightsCorrectionTargetDto(
    Guid ApplicationId,
    long Version,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department);

public sealed record WorkspaceStaffOnboardingDataRightsCorrectionReceiptDto(
    Guid ReceiptId,
    Guid ExecutionId,
    Guid CaseId,
    long ApprovalRevision,
    Guid ApplicationId,
    long SelectedRecordVersion,
    long CurrentRecordVersion,
    IReadOnlyCollection<string> ChangedFieldKeys,
    DateTimeOffset CompletedAtUtc);
