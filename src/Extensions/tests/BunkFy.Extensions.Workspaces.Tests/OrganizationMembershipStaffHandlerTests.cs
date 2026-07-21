namespace BunkFy.Extensions.Workspaces.Tests;

using BunkFy.Modules.Staff.Contracts;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationMembershipStaffHandlerTests
{
    [Fact]
    public async Task Active_member_membership_does_not_create_a_staff_profile()
    {
        RecordingStaffIdentityReconciler staff = new();
        RecordingContactReader contacts = new();
        OrganizationMembershipStaffHandler handler = new(
            staff,
            contacts,
            Options.Create(new BunkFyWorkspacesOptions { GlobalAuthScopeId = "global" }));

        await handler.HandleAsync(
            CreateEvent(OrganizationMembershipRole.Member, OrganizationMembershipStatus.Active),
            CancellationToken.None);

        Assert.Empty(staff.Requests);
        Assert.Equal(0, contacts.CallCount);
    }

    [Fact]
    public async Task Owner_membership_still_provisions_the_bootstrap_staff_profile()
    {
        RecordingStaffIdentityReconciler staff = new();
        RecordingContactReader contacts = new();
        OrganizationMembershipStaffHandler handler = new(
            staff,
            contacts,
            Options.Create(new BunkFyWorkspacesOptions { GlobalAuthScopeId = "global" }));

        OrganizationMembershipChangedIntegrationEvent integrationEvent = CreateEvent(
            OrganizationMembershipRole.Owner,
            OrganizationMembershipStatus.Active);
        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        StaffIdentityReconciliationRequest request = Assert.Single(staff.Requests);
        Assert.Equal(integrationEvent.SubjectId, request.AuthSubjectId);
        Assert.True(request.IsActive);
        Assert.Equal("owner@example.test", request.WorkEmail);
    }

    private static OrganizationMembershipChangedIntegrationEvent CreateEvent(
        OrganizationMembershipRole role,
        OrganizationMembershipStatus status)
    {
        Guid organizationId = Guid.NewGuid();
        return new OrganizationMembershipChangedIntegrationEvent(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero),
            organizationId.ToString("D"),
            organizationId,
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            OrganizationMembershipChange.Joined,
            role,
            status,
            1);
    }

    private sealed class RecordingStaffIdentityReconciler : IStaffIdentityReconciler
    {
        public List<StaffIdentityReconciliationRequest> Requests { get; } = [];

        public Task<StaffIdentityReconciliationResult> ReconcileAsync(
            StaffIdentityReconciliationRequest request,
            CancellationToken cancellationToken)
        {
            this.Requests.Add(request);
            return Task.FromResult(new StaffIdentityReconciliationResult(true, null));
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
