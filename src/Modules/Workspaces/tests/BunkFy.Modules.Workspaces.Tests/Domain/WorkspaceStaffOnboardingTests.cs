namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffOnboardingTests
{
    [Fact]
    public void Completion_requires_staff_and_redacts_applicant_data()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        Guid claimId = Guid.NewGuid();
        Guid staffMemberId = Guid.NewGuid();

        Assert.True(application.ObserveClaimAccepted(claimId, 1, Now.AddMinutes(1)).IsSuccess);
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
            Guid claimId = Guid.NewGuid();
            Assert.True(application.ObserveClaimRequested(claimId, 1, Now.AddMinutes(1)).IsSuccess);
            Assert.True(application.ObserveClaimRejected(claimId, 2, Now.AddMinutes(2)).IsSuccess);
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
        Guid claimId = Guid.NewGuid();
        Guid staffMemberId = Guid.NewGuid();
        Assert.True(application.ObserveClaimAccepted(claimId, 1, Now.AddMinutes(1)).IsSuccess);
        Assert.True(application.MarkStaffReady(staffMemberId, Now.AddMinutes(2)).IsSuccess);
        Assert.True(application.Fail("Workspaces.AccessProvisioningFailed", Now.AddMinutes(3)).IsSuccess);

        Assert.True(application.BeginProvisioning(Now.AddMinutes(4)).IsSuccess);

        Assert.Equal(WorkspaceStaffOnboardingState.Provisioning, application.Status);
        Assert.Equal(staffMemberId, application.StaffMemberId);
    }

    [Fact]
    public void Accepted_claim_records_the_authoritative_version()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        Guid claimId = Guid.NewGuid();

        Assert.True(application.ObserveClaimRequested(claimId, 1, Now.AddMinutes(1)).IsSuccess);
        Assert.True(application.ObserveClaimAccepted(claimId, 2, Now.AddMinutes(2)).IsSuccess);

        Assert.Equal(claimId, application.ClaimId);
        Assert.Equal(2, application.ClaimVersion);
        Assert.Equal(WorkspaceStaffOnboardingState.Provisioning, application.Status);
    }

    [Fact]
    public void Stale_requested_event_cannot_regress_an_accepted_claim()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        Guid claimId = Guid.NewGuid();
        Assert.True(application.ObserveClaimRequested(claimId, 1, Now.AddMinutes(1)).IsSuccess);
        Assert.True(application.ObserveClaimAccepted(claimId, 2, Now.AddMinutes(2)).IsSuccess);
        long aggregateVersion = application.Version;

        Assert.True(application.ObserveClaimRequested(claimId, 1, Now.AddMinutes(3)).IsSuccess);

        Assert.Equal(2, application.ClaimVersion);
        Assert.Equal(WorkspaceStaffOnboardingState.Provisioning, application.Status);
        Assert.Equal(aggregateVersion, application.Version);
    }

    [Fact]
    public void Stale_rejection_cannot_terminalize_failed_accepted_provisioning()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        Guid claimId = Guid.NewGuid();
        Assert.True(application.ObserveClaimRequested(claimId, 1, Now.AddMinutes(1)).IsSuccess);
        Assert.True(application.ObserveClaimAccepted(claimId, 2, Now.AddMinutes(2)).IsSuccess);
        Assert.True(application.Fail("Workspaces.AccessProvisioningFailed", Now.AddMinutes(3)).IsSuccess);
        long aggregateVersion = application.Version;

        Assert.True(application.ObserveClaimRejected(claimId, 1, Now.AddMinutes(4)).IsSuccess);

        Assert.Equal(2, application.ClaimVersion);
        Assert.Equal(WorkspaceStaffOnboardingState.Failed, application.Status);
        Assert.Equal(aggregateVersion, application.Version);
        Assert.NotNull(application.DisplayName);
    }

    [Fact]
    public void Duplicate_rejection_is_idempotent_and_keeps_applicant_data_redacted()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        Guid claimId = Guid.NewGuid();
        Assert.True(application.ObserveClaimRequested(claimId, 1, Now.AddMinutes(1)).IsSuccess);
        Assert.True(application.ObserveClaimRejected(claimId, 2, Now.AddMinutes(2)).IsSuccess);
        long aggregateVersion = application.Version;

        Assert.True(application.ObserveClaimRejected(claimId, 2, Now.AddMinutes(3)).IsSuccess);

        Assert.Equal(WorkspaceStaffOnboardingState.Rejected, application.Status);
        Assert.Equal(aggregateVersion, application.Version);
        AssertApplicantDataRedacted(application);
    }

    [Fact]
    public void Accepted_event_can_arrive_before_the_requested_event()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        Guid claimId = Guid.NewGuid();

        Assert.True(application.ObserveClaimAccepted(claimId, 1, Now.AddMinutes(1)).IsSuccess);
        long aggregateVersion = application.Version;
        Assert.True(application.ObserveClaimRequested(claimId, 1, Now.AddMinutes(2)).IsSuccess);

        Assert.Equal(1, application.ClaimVersion);
        Assert.Equal(WorkspaceStaffOnboardingState.Provisioning, application.Status);
        Assert.Equal(aggregateVersion, application.Version);
    }

    [Fact]
    public void Provisioning_requires_authoritative_source_acceptance()
    {
        WorkspaceStaffOnboarding application = CreateApplication();

        Result submitted = application.BeginProvisioning(Now.AddMinutes(1));
        Assert.True(submitted.IsFailure);
        Assert.Equal(WorkspaceStaffOnboardingState.Submitted, application.Status);

        Assert.True(application.ObserveClaimRequested(
            Guid.NewGuid(),
            1,
            Now.AddMinutes(2)).IsSuccess);
        Result pendingApproval = application.BeginProvisioning(Now.AddMinutes(3));
        Assert.True(pendingApproval.IsFailure);
        Assert.Equal(WorkspaceStaffOnboardingState.PendingApproval, application.Status);
    }

    [Fact]
    public void Pending_approval_freezes_the_profile_reviewed_by_the_owner()
    {
        WorkspaceStaffOnboarding application = CreateApplication();
        Assert.True(application.ObserveClaimRequested(
            Guid.NewGuid(),
            1,
            Now.AddMinutes(1)).IsSuccess);

        Result updated = application.UpdateSubmission(
            "verified@example.test",
            "Changed after review",
            null,
            null,
            null,
            null,
            null,
            null,
            Now.AddMinutes(2));

        Assert.True(updated.IsFailure);
        Assert.Equal(WorkspaceStaffOnboardingErrors.Unavailable, updated.Error);
        Assert.Equal("Ada Operator", application.DisplayName);
        Assert.Equal(WorkspaceStaffOnboardingState.PendingApproval, application.Status);
    }

    [Fact]
    public void Invitation_acceptance_opens_the_provisioning_path()
    {
        WorkspaceStaffOnboarding application = CreateApplication(
            WorkspaceStaffOnboardingSource.Invitation);

        Assert.True(application.ObserveInvitationAccepted(Now.AddMinutes(1)).IsSuccess);

        Assert.Equal(WorkspaceStaffOnboardingState.Provisioning, application.Status);
        Assert.True(application.BeginProvisioning(Now.AddMinutes(2)).IsSuccess);
    }

    internal static WorkspaceStaffOnboarding CreateApplication(
        WorkspaceStaffOnboardingSource sourceKind = WorkspaceStaffOnboardingSource.EnrollmentLink) =>
        WorkspaceStaffOnboarding.Create(
            Guid.NewGuid(),
            OrganizationId.ToString("D"),
            sourceKind,
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
