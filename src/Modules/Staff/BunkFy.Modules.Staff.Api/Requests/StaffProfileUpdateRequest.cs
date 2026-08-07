namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffProfileUpdateRequest(Guid OperationId, string DisplayName, string? LegalName,
    string? WorkEmail, string? WorkPhone, string? EmployeeNumber, string? JobTitle,
    string? Department, long ExpectedVersion);
