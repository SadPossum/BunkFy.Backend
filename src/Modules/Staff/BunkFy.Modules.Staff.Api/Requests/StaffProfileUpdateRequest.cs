namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffProfileUpdateRequest(string DisplayName, string? LegalName,
    string? WorkEmail, string? WorkPhone, string? EmployeeNumber, string? JobTitle,
    string? Department, long ExpectedVersion);
