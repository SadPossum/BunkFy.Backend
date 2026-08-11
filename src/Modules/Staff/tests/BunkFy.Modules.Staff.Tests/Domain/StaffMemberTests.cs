namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Models;
using BunkFy.Modules.Staff.Domain.ValueObjects;
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
    public void Creation_equivalence_uses_every_normalized_profile_value()
    {
        StaffMember member = Create(" ADA Operator ", " EMP-001 ", " user-42 ");
        StaffProfile same = StaffProfile.Create(
            "ADA Operator",
            legalName: null,
            " ADA@EXAMPLE.TEST ",
            workPhone: null,
            "EMP-001",
            " Manager ",
            " Operations ",
            "user-42").Value;
        StaffProfile different = StaffProfile.Create(
            "ADA Operator",
            legalName: null,
            "ada@example.test",
            workPhone: null,
            "EMP-001",
            "Supervisor",
            "Operations",
            "user-42").Value;

        Assert.True(member.MatchesCreation(same));
        Assert.False(member.MatchesCreation(different));
    }

    [Fact]
    public void Assignments_check_versions_before_no_ops_and_retain_history()
    {
        StaffMember member = Create("Ada", "EMP-1", null);
        Guid propertyId = Guid.NewGuid();

        Assert.True(member.AssignProperty(Guid.NewGuid(), propertyId, "Night Manager", true,
            new DateOnly(2026, 7, 1), 1, "user:owner", Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(2, member.Version);
        Assert.Single(member.Assignments);
        Assert.True(member.Assignments.Single().IsPrimary);

        Assert.Equal("Staff.VersionConflict", member.AssignProperty(Guid.NewGuid(), propertyId,
            "Night Manager", true, new DateOnly(2026, 7, 1), 1, "user:owner",
            Guid.NewGuid(), Now).Error.Code);
        Assert.True(member.AssignProperty(Guid.NewGuid(), propertyId, "Night Manager", true,
            new DateOnly(2026, 7, 1), 2, "user:owner", Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(2, member.Version);
        Assert.Equal("Staff.PrimaryAssignmentExists", member.AssignProperty(Guid.NewGuid(), Guid.NewGuid(),
            null, true, new DateOnly(2026, 7, 1), 2, "user:owner", Guid.NewGuid(), Now).Error.Code);

        Assert.True(member.UnassignProperty(propertyId, new DateOnly(2026, 7, 10), 2,
            "user:owner", "Transferred", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(3, member.Version);
        Assert.False(member.Assignments.Single().IsCurrent);
        Assert.Equal("Transferred", member.Assignments.Single().UnassignmentReason);

        Assert.Equal("Staff.VersionConflict", member.UnassignProperty(propertyId,
            new DateOnly(2026, 7, 10), 2, "user:owner", "Transferred",
            Guid.NewGuid(), Now.AddMinutes(1)).Error.Code);
        Assert.Equal("Staff.AssignmentNotFound", member.UnassignProperty(propertyId,
            new DateOnly(2026, 7, 10), 3, "user:owner", "Transferred",
            Guid.NewGuid(), Now.AddMinutes(1)).Error.Code);
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
    public void Anonymisation_scrubs_profile_and_assignment_free_text()
    {
        StaffMember member = StaffMember.Create(
            Guid.NewGuid(),
            "tenant-a",
            "Ada Operator",
            "Ada Lovelace",
            "ada@example.test",
            "+44 20 1234",
            "EMP-1",
            "Manager",
            "Operations",
            "account-1",
            "user:owner",
            Guid.NewGuid(),
            Now).Value;
        Guid propertyId = Guid.NewGuid();
        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            "Night Manager",
            isPrimary: true,
            new DateOnly(2026, 7, 1),
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(member.Depart(
            new DateOnly(2026, 7, 12),
            member.Version,
            "user:owner",
            "Contract ended",
            Guid.NewGuid(),
            [Guid.NewGuid()],
            Now.AddHours(1)).IsSuccess);
        long selectedVersion = member.Version;
        Guid eventId = Guid.NewGuid();

        var result = member.Anonymise(
            selectedVersion,
            "user:privacy",
            eventId,
            Now.AddHours(2));

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(selectedVersion + 1, member.Version);
        Assert.Equal(StaffMemberState.Anonymised, member.Status);
        Assert.Equal(
            StaffMember.AnonymisedDisplayName,
            member.DisplayName);
        Assert.Equal(
            StaffMember.AnonymisedDisplayName.ToUpperInvariant(),
            member.DisplayNameSearch);
        Assert.Null(member.LegalName);
        Assert.Null(member.WorkEmail);
        Assert.Null(member.WorkPhone);
        Assert.Null(member.EmployeeNumber);
        Assert.Null(member.JobTitle);
        Assert.Null(member.Department);
        Assert.Null(member.AuthSubjectId);
        Assert.Equal(propertyId, member.Assignments.Single().PropertyId);
        Assert.False(member.Assignments.Single().IsCurrent);
        Assert.Null(member.Assignments.Single().PropertyJobTitle);
        Assert.Null(member.Assignments.Single().UnassignmentReason);
        Assert.True(member.MatchesAnonymisedState(
            member.Version,
            Now.AddHours(2)));
        Assert.Equal(
            "Staff.StaffAnonymised",
            member.ApplyDataRightsCorrection(
                StaffProfileCorrection.Create(
                    "Changed",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null).Value,
                member.Version,
                "user:privacy",
                Guid.NewGuid(),
                Now.AddHours(3)).Error.Code);
    }

    [Fact]
    public void Anonymisation_requires_departure_and_no_current_assignment()
    {
        StaffMember member = Create("Ada", "EMP-1", "account-1");

        Assert.Equal(
            "Staff.AnonymisationTransitionInvalid",
            member.Anonymise(
                member.Version,
                "user:privacy",
                Guid.NewGuid(),
                Now.AddHours(1)).Error.Code);
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

    [Fact]
    public void Immediate_transitions_reject_future_effective_dates_without_mutation()
    {
        StaffMember member = Create("Ada", null, null);
        Guid propertyId = Guid.NewGuid();
        DateOnly today = DateOnly.FromDateTime(Now.UtcDateTime);
        DateOnly tomorrow = today.AddDays(1);
        int initialEventCount = member.DomainEvents.Count;

        Assert.Equal(
            "Staff.AssignmentDateInvalid",
            member.AssignProperty(
                Guid.NewGuid(),
                propertyId,
                null,
                false,
                tomorrow,
                member.Version,
                "user:owner",
                Guid.NewGuid(),
                Now).Error.Code);
        Assert.Equal(1, member.Version);
        Assert.Empty(member.Assignments);
        Assert.Equal(initialEventCount, member.DomainEvents.Count);

        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            null,
            false,
            today,
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            Now).IsSuccess);
        long assignedVersion = member.Version;
        int assignedEventCount = member.DomainEvents.Count;

        Assert.Equal(
            "Staff.AssignmentDateInvalid",
            member.UnassignProperty(
                propertyId,
                tomorrow,
                member.Version,
                "user:owner",
                "Transfer scheduled.",
                Guid.NewGuid(),
                Now).Error.Code);
        Assert.Equal(
            "Staff.AssignmentDateInvalid",
            member.Depart(
                tomorrow,
                member.Version,
                "user:owner",
                "Departure scheduled.",
                Guid.NewGuid(),
                [Guid.NewGuid()],
                Now).Error.Code);
        Assert.Equal(assignedVersion, member.Version);
        Assert.Equal(StaffMemberState.Active, member.Status);
        Assert.True(Assert.Single(member.Assignments).IsCurrent);
        Assert.Equal(assignedEventCount, member.DomainEvents.Count);
    }

    [Fact]
    public void Reassignment_cannot_overlap_same_property_history()
    {
        StaffMember member = Create("Ada", null, null);
        Guid propertyId = Guid.NewGuid();
        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            null,
            false,
            new DateOnly(2026, 7, 1),
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            Now).IsSuccess);
        Assert.True(member.UnassignProperty(
            propertyId,
            new DateOnly(2026, 7, 10),
            member.Version,
            "user:owner",
            "Transferred.",
            Guid.NewGuid(),
            Now).IsSuccess);
        long endedVersion = member.Version;
        int endedEventCount = member.DomainEvents.Count;

        Assert.Equal(
            "Staff.AssignmentDateInvalid",
            member.AssignProperty(
                Guid.NewGuid(),
                propertyId,
                null,
                false,
                new DateOnly(2026, 7, 10),
                member.Version,
                "user:owner",
                Guid.NewGuid(),
                Now).Error.Code);
        Assert.Equal(endedVersion, member.Version);
        Assert.Equal(endedEventCount, member.DomainEvents.Count);

        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            null,
            false,
            new DateOnly(2026, 7, 11),
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            Now).IsSuccess);
        Assert.Equal(2, member.Assignments.Count);
        Assert.Single(member.Assignments, assignment => assignment.IsCurrent);
    }

    [Fact]
    public void Approved_correction_updates_departed_profile_without_changing_lifecycle()
    {
        StaffMember member = Create("Ada", "EMP-1", "account-1");
        Guid propertyId = Guid.NewGuid();
        Assert.True(member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            "Manager",
            isPrimary: true,
            new DateOnly(2026, 7, 1),
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            Now).IsSuccess);
        Assert.True(member.Depart(
            new DateOnly(2026, 7, 12),
            member.Version,
            "user:owner",
            "Contract ended",
            Guid.NewGuid(),
            [Guid.NewGuid()],
            Now.AddHours(1)).IsSuccess);
        long selectedVersion = member.Version;
        StaffProfileCorrection correction = StaffProfileCorrection.Create(
            " Ada Lovelace ",
            "Augusta Ada King",
            "ADA.NEW@EXAMPLE.TEST",
            workPhone: null,
            "EMP-2",
            "Principal Manager",
            "Operations").Value;

        var corrected = member.ApplyDataRightsCorrection(
            correction,
            selectedVersion,
            "user:privacy",
            Guid.NewGuid(),
            Now.AddHours(2));

        Assert.True(corrected.IsSuccess, corrected.Error.Code);
        Assert.Equal(selectedVersion + 1, member.Version);
        Assert.Equal(StaffMemberState.Departed, member.Status);
        Assert.Equal("account-1", member.AuthSubjectId);
        Assert.Equal("ADA LOVELACE", member.DisplayNameSearch);
        Assert.Equal("ada.new@example.test", member.WorkEmail);
        Assert.False(member.Assignments.Single().IsCurrent);
        Assert.Equal(
            [
                StaffProfileField.DisplayName,
                StaffProfileField.LegalName,
                StaffProfileField.WorkEmail,
                StaffProfileField.EmployeeNumber,
                StaffProfileField.JobTitle
            ],
            corrected.Value.ChangedFields);
    }

    [Fact]
    public void Approved_correction_rejects_stale_or_no_op_profile()
    {
        StaffMember member = Create("Ada", "EMP-1", "account-1");
        StaffProfileCorrection same = StaffProfileCorrection.Create(
            member.DisplayName,
            member.LegalName,
            member.WorkEmail,
            member.WorkPhone,
            member.EmployeeNumber,
            member.JobTitle,
            member.Department).Value;

        Assert.Equal(
            "Staff.CorrectionNoChanges",
            member.ApplyDataRightsCorrection(
                same,
                member.Version,
                "user:privacy",
                Guid.NewGuid(),
                Now.AddMinutes(1)).Error.Code);
        Assert.Equal(
            "Staff.VersionConflict",
            member.ApplyDataRightsCorrection(
                same,
                expectedVersion: 99,
                "user:privacy",
                Guid.NewGuid(),
                Now.AddMinutes(1)).Error.Code);
    }

    private static StaffMember Create(string name, string? employeeNumber, string? subject) =>
        StaffMember.Create(Guid.NewGuid(), "tenant-a", name, null, "ADA@Example.Test", null,
            employeeNumber, "Manager", "Operations", subject, " user:owner ", Guid.NewGuid(), Now).Value;
}
