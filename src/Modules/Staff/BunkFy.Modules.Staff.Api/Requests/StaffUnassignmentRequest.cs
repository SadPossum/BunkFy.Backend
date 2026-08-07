namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffUnassignmentRequest(Guid OperationId, DateOnly EffectiveTo, string Reason,
    long ExpectedVersion);
