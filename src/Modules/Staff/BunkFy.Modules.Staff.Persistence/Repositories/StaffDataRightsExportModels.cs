namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed record StaffProfileDataRightsExport(
    Guid StaffMemberId,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department,
    string? AuthSubjectId,
    StaffMemberState Status,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    DateTimeOffset? DepartedAtUtc,
    DateOnly? DepartureEffectiveOn);

internal sealed record StaffAssignmentDataRightsExport(
    Guid AssignmentId,
    Guid StaffMemberId,
    Guid PropertyId,
    string? PropertyJobTitle,
    bool IsPrimary,
    bool IsCurrent,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    DateTimeOffset AssignedAtUtc,
    long AssignedAtVersion,
    DateTimeOffset? UnassignedAtUtc,
    long? UnassignedAtVersion)
{
    public long RecordVersion => this.UnassignedAtVersion ?? this.AssignedAtVersion;
}
