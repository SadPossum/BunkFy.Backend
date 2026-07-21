namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Domain;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffOnboardingTests
{
    [Fact]
    public void Completion_requires_staff_and_redacts_applicant_data()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        Guid staffMemberId = Guid.NewGuid();

        Assert.True(application.BeginProvisioning(Now.AddMinutes(1)).IsSuccess);
        Assert.True(application.MarkStaffReady(staffMemberId, Now.AddMinutes(2)).IsSuccess);
        Assert.True(application.Complete(Now.AddMinutes(3)).IsSuccess);

        Assert.Equal(WorkspaceStaffOnboardingState.Completed, application.Status);
        Assert.Equal(staffMemberId, application.StaffMemberId);
        AssertApplicantDataRedacted(application);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Terminal_denial_states_redact_applicant_data(bool reject)
    {
        WorkspaceStaffOnboarding application = CreateApplication();

        if (reject)
        {
            Assert.True(application.BindClaim(Guid.NewGuid(), 1, Now.AddMinutes(1)).IsSuccess);
            Assert.True(application.Reject(2, Now.AddMinutes(2)).IsSuccess);
            Assert.Equal(WorkspaceStaffOnboardingState.Rejected, application.Status);
        }
        else
        {
            Assert.True(application.Supersede(Now.AddMinutes(1)).IsSuccess);
            Assert.Equal(WorkspaceStaffOnboardingState.Superseded, application.Status);
        }

        AssertApplicantDataRedacted(application);
    }

    [Fact]
    public void Failed_staff_ready_work_can_reenter_provisioning_without_losing_staff_identity()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        Guid staffMemberId = Guid.NewGuid();
        Assert.True(application.BeginProvisioning(Now.AddMinutes(1)).IsSuccess);
        Assert.True(application.MarkStaffReady(staffMemberId, Now.AddMinutes(2)).IsSuccess);
        Assert.True(application.Fail("Workspaces.AccessProvisioningFailed", Now.AddMinutes(3)).IsSuccess);

        Assert.True(application.BeginProvisioning(Now.AddMinutes(4)).IsSuccess);

        Assert.Equal(WorkspaceStaffOnboardingState.Provisioning, application.Status);
        Assert.Equal(staffMemberId, application.StaffMemberId);
    }

    internal static WorkspaceStaffOnboarding CreateApplication() => WorkspaceStaffOnboarding.Create(
        Guid.NewGuid(),
        OrganizationId.ToString("D"),
        WorkspaceStaffOnboardingSource.EnrollmentLink,
        Guid.NewGuid(),
        SubjectId,
        "verified@example.test",
        "Ada Operator",
        "Ada Lovelace",
        "ada@workspace.test",
        "+1 555 0100",
        "EMP-100",
        "Manager",
        "Operations",
        Now).Value;

    internal static readonly Guid OrganizationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly string SubjectId = Guid.Parse("20000000-0000-0000-0000-000000000002").ToString("D");
    internal static readonly DateTimeOffset Now = new(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);

    private static void AssertApplicantDataRedacted(WorkspaceStaffOnboarding application)
    {
        Assert.Null(application.VerifiedAccountEmail);
        Assert.Null(application.DisplayName);
        Assert.Null(application.LegalName);
        Assert.Null(application.WorkEmail);
        Assert.Null(application.WorkPhone);
        Assert.Null(application.EmployeeNumber);
        Assert.Null(application.JobTitle);
        Assert.Null(application.Department);
    }
}
