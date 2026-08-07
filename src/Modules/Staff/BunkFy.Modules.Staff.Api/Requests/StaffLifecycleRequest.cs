namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffLifecycleRequest(
    Guid OperationId,
    string Reason,
    long ExpectedVersion);
