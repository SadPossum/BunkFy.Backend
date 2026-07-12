namespace BunkFy.Modules.Staff.Api.Requests;

public sealed record StaffAssignmentRequest(string? PropertyJobTitle, bool IsPrimary,
    DateOnly EffectiveFrom, long ExpectedVersion);
