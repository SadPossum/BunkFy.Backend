namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record ReleaseStaffDataHoldRequest(
    Guid IdempotencyKey,
    long ExpectedStaffVersion,
    long ExpectedHoldVersion,
    bool Confirmed);
