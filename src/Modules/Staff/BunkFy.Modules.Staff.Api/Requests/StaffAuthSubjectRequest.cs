namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffAuthSubjectRequest(string? AuthSubjectId, long ExpectedVersion);
