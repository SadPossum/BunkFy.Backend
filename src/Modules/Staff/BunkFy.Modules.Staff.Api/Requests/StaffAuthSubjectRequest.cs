namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffAuthSubjectRequest(
    Guid OperationId,
    string? AuthSubjectId,
    long ExpectedVersion);
