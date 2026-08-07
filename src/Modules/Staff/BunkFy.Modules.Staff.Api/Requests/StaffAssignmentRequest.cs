namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffAssignmentRequest(Guid OperationId, string? PropertyJobTitle, bool IsPrimary,
    DateOnly EffectiveFrom, long ExpectedVersion);
