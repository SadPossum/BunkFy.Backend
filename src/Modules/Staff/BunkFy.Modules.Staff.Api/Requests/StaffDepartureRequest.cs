namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffDepartureRequest(DateOnly EffectiveOn, string Reason, long ExpectedVersion);
