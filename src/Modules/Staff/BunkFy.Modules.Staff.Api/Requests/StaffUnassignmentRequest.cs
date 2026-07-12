namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffUnassignmentRequest(DateOnly EffectiveTo, string Reason, long ExpectedVersion);
