namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffLifecycleRequest(string Reason, long ExpectedVersion);
