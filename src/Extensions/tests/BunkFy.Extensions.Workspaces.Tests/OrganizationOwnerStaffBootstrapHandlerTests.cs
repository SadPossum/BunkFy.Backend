namespace BunkFy.Extensions.Workspaces.Tests;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationOwnerStaffBootstrapHandlerTests
{
    [Fact]
    public async Task Active_member_membership_does_not_create_a_staff_profile()
    {
        RecordingStaffIdentityBootstrapper staff = new();
        RecordingContactReader contacts = new();
        StubOrganizationAccessDecisionReader access = new();
        OrganizationOwnerStaffBootstrapHandler handler = new(
            staff,
            contacts,
            access,
            new StubWorkspaceOperationalAdmissionPolicy(),
            Options.Create(new BunkFyWorkspacesOptions { GlobalAuthScopeId = "global" }));

        await handler.HandleAsync(
            CreateEvent(OrganizationMembershipRole.Member, OrganizationMembershipStatus.Active),
            CancellationToken.None);

        Assert.Empty(staff.Requests);
        Assert.Equal(0, contacts.CallCount);
        Assert.Equal(0, access.CallCount);
    }

    [Fact]
    public async Task Owner_membership_still_provisions_the_bootstrap_staff_profile()
    {
        RecordingStaffIdentityBootstrapper staff = new();
        RecordingContactReader contacts = new();
        StubOrganizationAccessDecisionReader access = new();
        OrganizationOwnerStaffBootstrapHandler handler = new(
            staff,
            contacts,
            access,
            new StubWorkspaceOperationalAdmissionPolicy(),
            Options.Create(new BunkFyWorkspacesOptions { GlobalAuthScopeId = "global" }));

        OrganizationMembershipChangedIntegrationEvent integrationEvent = CreateEvent(
            OrganizationMembershipRole.Owner,
            OrganizationMembershipStatus.Active);
        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        StaffIdentityBootstrapRequest request = Assert.Single(staff.Requests);
        Assert.Equal(integrationEvent.EventId, request.OperationId);
        Assert.Equal(integrationEvent.SubjectId, request.AuthSubjectId);
        Assert.Equal("owner@example.test", request.WorkEmail);
        Assert.Equal(1, access.CallCount);
    }

    [Theory]
    [InlineData(OrganizationMembershipChange.Suspended, OrganizationMembershipStatus.Suspended)]
    [InlineData(OrganizationMembershipChange.Resumed, OrganizationMembershipStatus.Active)]
    [InlineData(OrganizationMembershipChange.Removed, OrganizationMembershipStatus.Removed)]
    public async Task Membership_lifecycle_changes_do_not_mutate_staff(
        OrganizationMembershipChange change,
        OrganizationMembershipStatus status)
    {
        RecordingStaffIdentityBootstrapper staff = new();
        RecordingContactReader contacts = new();
        OrganizationOwnerStaffBootstrapHandler handler = new(
            staff,
            contacts,
            new StubOrganizationAccessDecisionReader(),
            new StubWorkspaceOperationalAdmissionPolicy(),
            Options.Create(new BunkFyWorkspacesOptions { GlobalAuthScopeId = "global" }));

        await handler.HandleAsync(
            CreateEvent(OrganizationMembershipRole.Owner, status, change),
            CancellationToken.None);

        Assert.Empty(staff.Requests);
        Assert.Equal(0, contacts.CallCount);
    }

    [Fact]
    public async Task Restricted_workspace_retries_owner_bootstrap_without_side_effects()
    {
        RecordingStaffIdentityBootstrapper staff = new();
        RecordingContactReader contacts = new();
        OrganizationOwnerStaffBootstrapHandler handler = new(
            staff,
            contacts,
            new StubOrganizationAccessDecisionReader(),
            new StubWorkspaceOperationalAdmissionPolicy(
                WorkspaceOperationalAdmissionOutcome.Restricted),
            Options.Create(new BunkFyWorkspacesOptions { GlobalAuthScopeId = "global" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(
                CreateEvent(
                    OrganizationMembershipRole.Owner,
                    OrganizationMembershipStatus.Active),
                CancellationToken.None));

        Assert.Empty(staff.Requests);
        Assert.Equal(0, contacts.CallCount);
    }

    [Theory]
    [InlineData(OrganizationAccessDecision.OrganizationNotFound)]
    [InlineData(OrganizationAccessDecision.OrganizationInactive)]
    [InlineData(OrganizationAccessDecision.MembershipNotFound)]
    [InlineData(OrganizationAccessDecision.MembershipInactive)]
    public async Task Stale_owner_event_is_acknowledged_before_contact_lookup(
        OrganizationAccessDecision decision)
    {
        RecordingStaffIdentityBootstrapper staff = new();
        RecordingContactReader contacts = new();
        OrganizationOwnerStaffBootstrapHandler handler = new(
            staff,
            contacts,
            new StubOrganizationAccessDecisionReader(decision),
            new StubWorkspaceOperationalAdmissionPolicy(),
            Options.Create(new BunkFyWorkspacesOptions
            {
                GlobalAuthScopeId = "global"
            }));

        await handler.HandleAsync(
            CreateEvent(
                OrganizationMembershipRole.Owner,
                OrganizationMembershipStatus.Active),
            CancellationToken.None);

        Assert.Empty(staff.Requests);
        Assert.Equal(0, contacts.CallCount);
    }

    [Theory]
    [InlineData(OrganizationAccessDecision.Unknown)]
    [InlineData(OrganizationAccessDecision.Unavailable)]
    public async Task Unavailable_owner_authority_retries_without_side_effects(
        OrganizationAccessDecision decision)
    {
        RecordingStaffIdentityBootstrapper staff = new();
        RecordingContactReader contacts = new();
        OrganizationOwnerStaffBootstrapHandler handler = new(
            staff,
            contacts,
            new StubOrganizationAccessDecisionReader(decision),
            new StubWorkspaceOperationalAdmissionPolicy(),
            Options.Create(new BunkFyWorkspacesOptions
            {
                GlobalAuthScopeId = "global"
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(
                CreateEvent(
                    OrganizationMembershipRole.Owner,
                    OrganizationMembershipStatus.Active),
                CancellationToken.None));

        Assert.Empty(staff.Requests);
        Assert.Equal(0, contacts.CallCount);
    }

    private static OrganizationMembershipChangedIntegrationEvent CreateEvent(
        OrganizationMembershipRole role,
        OrganizationMembershipStatus status,
        OrganizationMembershipChange change = OrganizationMembershipChange.Joined)
    {
        Guid organizationId = Guid.NewGuid();
        return new OrganizationMembershipChangedIntegrationEvent(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero),
            organizationId.ToString("D"),
            organizationId,
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            change,
            role,
            status,
            1);
    }

    private sealed class RecordingStaffIdentityBootstrapper
        : IStaffIdentityBootstrapper
    {
        public List<StaffIdentityBootstrapRequest> Requests { get; } = [];

        public Task<StaffIdentityBootstrapResult> BootstrapAsync(
            StaffIdentityBootstrapRequest request,
            CancellationToken cancellationToken)
        {
            this.Requests.Add(request);
            return Task.FromResult(new StaffIdentityBootstrapResult(true, null));
        }
    }

    private sealed class StubOrganizationAccessDecisionReader(
        OrganizationAccessDecision decision = OrganizationAccessDecision.Allowed)
        : IOrganizationAccessDecisionReader
    {
        public int CallCount { get; private set; }

        public Task<OrganizationAccessDecision> ReadAsync(
            Guid organizationId,
            string subjectId,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            return Task.FromResult(decision);
        }
    }

    private sealed class RecordingContactReader : IAuthMemberContactReader
    {
        public int CallCount { get; private set; }

        public ValueTask<string?> GetPreferredVerifiedEmailAsync(
            string scopeId,
            Guid memberId,
            CancellationToken cancellationToken = default)
        {
            this.CallCount++;
            return ValueTask.FromResult<string?>("owner@example.test");
        }
    }
}
