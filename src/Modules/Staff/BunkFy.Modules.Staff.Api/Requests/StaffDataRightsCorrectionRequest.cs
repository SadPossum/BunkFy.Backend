namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffDataRightsCorrectionRequest(
    Guid ExecutionId,
    Guid CaseId,
    long ApprovalRevision,
    Guid StaffMemberId,
    long ExpectedVersion,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department);
