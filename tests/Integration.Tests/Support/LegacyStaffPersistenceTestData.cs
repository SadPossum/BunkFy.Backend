namespace Integration.Tests.Support;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class LegacyStaffPersistenceTestData
{
    public static async Task InsertMemberAsync(
        StaffDbContext dbContext,
        StaffMember member,
        CancellationToken cancellationToken = default)
    {
        StaffPropertyAssignment assignment = member.Assignments.Single();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO staff.staff_members (
                "Id", "AuthSubjectId", "CreatedAtUtc", "CreatedBy",
                "DepartedAtUtc", "Department", "DepartureEffectiveOn",
                "DisplayName", "DisplayNameSearch", "EmployeeNumber",
                "EmployeeNumberSearch", "JobTitle", "LastChangedAtUtc",
                "LastChangedBy", "LegalName", "LegalNameSearch", "ScopeId",
                "Status", "SuspendedAtUtc", "Version", "WorkEmail",
                "WorkEmailSearch", "WorkPhone", "WorkPhoneSearch")
            VALUES (
                {member.Id}, {member.AuthSubjectId}, {member.CreatedAtUtc},
                {member.CreatedBy}, {member.DepartedAtUtc}, {member.Department},
                {member.DepartureEffectiveOn}, {member.DisplayName},
                {member.DisplayNameSearch}, {member.EmployeeNumber},
                {member.EmployeeNumberSearch}, {member.JobTitle},
                {member.LastChangedAtUtc}, {member.LastChangedBy},
                {member.LegalName}, {member.LegalNameSearch}, {member.ScopeId},
                {(int)member.Status}, {member.SuspendedAtUtc}, {member.Version},
                {member.WorkEmail}, {member.WorkEmailSearch}, {member.WorkPhone},
                {member.WorkPhoneSearch});

            INSERT INTO staff.property_assignments (
                "ScopeId", "StaffMemberId", "Id", "AssignedAtUtc",
                "AssignedAtVersion", "AssignedBy", "EffectiveFrom",
                "EffectiveTo", "IsCurrent", "IsPrimary", "PropertyId",
                "PropertyJobTitle", "UnassignedAtUtc", "UnassignedAtVersion",
                "UnassignedBy", "UnassignmentReason")
            VALUES (
                {assignment.ScopeId}, {assignment.StaffMemberId},
                {assignment.Id}, {assignment.AssignedAtUtc},
                {assignment.AssignedAtVersion}, {assignment.AssignedBy},
                {assignment.EffectiveFrom}, {assignment.EffectiveTo},
                {assignment.IsCurrent}, {assignment.IsPrimary},
                {assignment.PropertyId}, {assignment.PropertyJobTitle},
                {assignment.UnassignedAtUtc}, {assignment.UnassignedAtVersion},
                {assignment.UnassignedBy}, {assignment.UnassignmentReason});
            """,
            cancellationToken).ConfigureAwait(false);
    }
}
