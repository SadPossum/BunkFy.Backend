namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffSelfProfileUpdateRequest(
    Guid OperationId,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? JobTitle,
    string? Department,
    long ExpectedVersion);
