namespace BunkFy.Modules.Staff.Application.Mapping;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Entities;

public static class StaffMappings
{
    public static StaffMemberDto ToDto(this StaffMember member) => new(
        member.Id,
        member.DisplayName,
        member.LegalName,
        member.WorkEmail,
        member.WorkPhone,
        member.EmployeeNumber,
        member.JobTitle,
        member.Department,
        member.AuthSubjectId,
        MapStatus(member.Status),
        member.Version,
        member.CreatedAtUtc,
        member.LastChangedAtUtc,
        member.SuspendedAtUtc,
        member.DepartedAtUtc,
        member.Assignments.OrderByDescending(item => item.IsCurrent)
            .ThenByDescending(item => item.EffectiveFrom)
            .ThenBy(item => item.Id)
            .Select(ToDto)
            .ToArray());

    public static StaffPropertyAssignmentDto ToDto(StaffPropertyAssignment assignment) => new(
        assignment.Id, assignment.PropertyId, assignment.PropertyJobTitle, assignment.IsPrimary,
        assignment.IsCurrent, assignment.EffectiveFrom, assignment.EffectiveTo,
        assignment.AssignedAtUtc, assignment.UnassignedAtUtc,
        assignment.AssignedAtVersion, assignment.UnassignedAtVersion);

    public static StaffStatus MapStatus(StaffMemberState status) => status switch
    {
        StaffMemberState.Active => StaffStatus.Active,
        StaffMemberState.Suspended => StaffStatus.Suspended,
        StaffMemberState.Departed => StaffStatus.Departed,
        _ => StaffStatus.Unknown
    };
}
