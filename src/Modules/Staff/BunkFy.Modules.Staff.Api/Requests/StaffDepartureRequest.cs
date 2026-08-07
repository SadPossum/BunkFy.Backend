namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffDepartureRequest(
    Guid OperationId,
    DateOnly EffectiveOn,
    string Reason,
    long ExpectedVersion);
