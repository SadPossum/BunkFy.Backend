namespace BunkFy.Modules.Staff.Contracts;

public sealed record StaffPropertyAssignmentDto(
    Guid AssignmentId,
    Guid PropertyId,
    string? PropertyJobTitle,
    bool IsPrimary,
    bool IsCurrent,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? UnassignedAtUtc,
    long AssignedAtVersion,
    long? UnassignedAtVersion);
