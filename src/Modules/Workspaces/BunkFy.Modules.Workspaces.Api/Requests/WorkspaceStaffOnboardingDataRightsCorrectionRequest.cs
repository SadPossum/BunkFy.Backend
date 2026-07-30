namespace BunkFy.Modules.Workspaces.Api.Requests;

public sealed record WorkspaceStaffOnboardingDataRightsCorrectionRequest(
    Guid ExecutionId,
    Guid CaseId,
    long ApprovalRevision,
    Guid ApplicationId,
    long ExpectedVersion,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department);
