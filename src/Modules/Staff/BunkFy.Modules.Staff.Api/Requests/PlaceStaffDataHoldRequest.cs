namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record PlaceStaffDataHoldRequest(
    Guid IdempotencyKey,
    long ExpectedStaffVersion,
    string ReasonCode);
