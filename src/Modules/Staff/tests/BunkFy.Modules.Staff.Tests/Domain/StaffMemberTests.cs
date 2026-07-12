namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffMemberTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_profile_and_keeps_auth_subject_as_optional_correlation()
    {
        StaffMember member = Create(" ADA Operator ", " EMP-001 ", " user-42 ");

        Assert.Equal("ADA Operator", member.DisplayName);
        Assert.Equal("ADA OPERATOR", member.DisplayNameSearch);
        Assert.Equal("ada@example.test", member.WorkEmail);
        Assert.Equal("EMP-001", member.EmployeeNumber);
        Assert.Equal("user-42", member.AuthSubjectId);
        Assert.Equal(StaffMemberState.Active, member.Status);
        Assert.Equal(1, member.Version);
        Assert.Empty(member.Assignments);
    }

    [Fact]
    public void Assignments_are_versioned_idempotent_and_retain_history()
    {
        StaffMember member = Create("Ada", "EMP-1", null);
        Guid propertyId = Guid.NewGuid();

        Assert.True(member.AssignProperty(Guid.NewGuid(), propertyId, "Night Manager", true,
            new DateOnly(2026, 7, 1), 1, "user:owner", Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(2, member.Version);
        Assert.Single(member.Assignments);
        Assert.True(member.Assignments.Single().IsPrimary);

        Assert.True(member.AssignProperty(Guid.NewGuid(), propertyId, "Night Manager", true,
            new DateOnly(2026, 7, 1), 1, "user:owner", Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(2, member.Version);
        Assert.Equal("Staff.PrimaryAssignmentExists", member.AssignProperty(Guid.NewGuid(), Guid.NewGuid(),
            null, true, new DateOnly(2026, 7, 1), 2, "user:owner", Guid.NewGuid(), Now).Error.Code);

        Assert.True(member.UnassignProperty(propertyId, new DateOnly(2026, 7, 10), 2,
            "user:owner", "Transferred", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(3, member.Version);
        Assert.False(member.Assignments.Single().IsCurrent);
        Assert.Equal("Transferred", member.Assignments.Single().UnassignmentReason);

        Assert.True(member.UnassignProperty(propertyId, new DateOnly(2026, 7, 10), 2,
            "user:owner", "Transferred", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(3, member.Version);
    }

    [Fact]
    public void Suspension_is_reversible_but_departure_is_terminal_and_ends_assignments()
    {
        StaffMember member = Create("Ada", null, null);
        Guid propertyId = Guid.NewGuid();
        member.AssignProperty(Guid.NewGuid(), propertyId, null, false, new DateOnly(2026, 7, 1),
            1, "user:owner", Guid.NewGuid(), Now);

        Assert.True(member.Suspend(2, "user:owner", "Leave", Guid.NewGuid(), Now.AddHours(1)).IsSuccess);
        Assert.Equal("Staff.StaffSuspended", member.AssignProperty(Guid.NewGuid(), Guid.NewGuid(), null,
            false, new DateOnly(2026, 7, 2), 3, "user:owner", Guid.NewGuid(), Now).Error.Code);
        Assert.True(member.Resume(3, "user:owner", "Returned", Guid.NewGuid(), Now.AddHours(2)).IsSuccess);
        Assert.True(member.Depart(new DateOnly(2026, 7, 12), 4, "user:owner", "Contract ended",
            Guid.NewGuid(), [Guid.NewGuid()], Now.AddHours(3)).IsSuccess);

        Assert.Equal(StaffMemberState.Departed, member.Status);
        Assert.Equal(5, member.Version);
        Assert.False(member.Assignments.Single().IsCurrent);
        Assert.Equal("Staff.StaffDeparted", member.UpdateProfile("Ada", null, null, null, null,
            null, null, 5, "user:owner", Guid.NewGuid(), Now.AddHours(4)).Error.Code);
    }

    [Fact]
    public void Stale_versions_and_invalid_identity_values_are_rejected()
    {
        StaffMember member = Create("Ada", null, null);
        Assert.Equal("Staff.VersionConflict", member.SetAuthSubject("user-2", 99,
            "user:owner", Guid.NewGuid(), Now).Error.Code);
        Assert.Equal("Staff.EmailInvalid", StaffMember.Create(Guid.NewGuid(), "tenant-a", "Ada",
            null, "not-an-email", null, null, null, null, null,
            "user:owner", Guid.NewGuid(), Now).Error.Code);
    }

    [Fact]
    public void Effective_dates_are_required_at_the_domain_boundary()
    {
        StaffMember member = Create("Ada", null, null);

        Assert.Equal("Staff.AssignmentDateInvalid", member.AssignProperty(Guid.NewGuid(), Guid.NewGuid(),
            null, false, default, member.Version, "user:owner", Guid.NewGuid(), Now).Error.Code);
        Assert.Equal("Staff.AssignmentDateInvalid", member.UnassignProperty(Guid.NewGuid(), default,
            member.Version, "user:owner", "Transferred", Guid.NewGuid(), Now).Error.Code);
        Assert.Equal("Staff.AssignmentDateInvalid", member.Depart(default, member.Version,
            "user:owner", "Contract ended", Guid.NewGuid(), [], Now).Error.Code);
    }

    private static StaffMember Create(string name, string? employeeNumber, string? subject) =>
        StaffMember.Create(Guid.NewGuid(), "tenant-a", name, null, "ADA@Example.Test", null,
            employeeNumber, "Manager", "Operations", subject, " user:owner ", Guid.NewGuid(), Now).Value;
}
