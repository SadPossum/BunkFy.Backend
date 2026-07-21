namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffDirectoryAssignmentDto(
    Guid AssignmentId,
    Guid PropertyId,
    string? PropertyJobTitle,
    bool IsPrimary,
    DateOnly EffectiveFrom);
