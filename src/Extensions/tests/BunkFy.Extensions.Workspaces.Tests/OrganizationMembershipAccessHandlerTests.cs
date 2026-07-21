namespace BunkFy.Extensions.Workspaces.Tests;

using Gma.Framework.AccessControl;
using Gma.Framework.Messaging;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Organizations.Contracts;
using Gma.Framework.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Contracts;

[Trait("Category", "Unit")]
public sealed class OrganizationMembershipAccessHandlerTests
{
    [Fact]
    public void Workspace_subscriptions_preserve_scope_aware_event_metadata()
    {
        ServiceCollection services = new();

        services.AddBunkFyWorkspaces();

        IntegrationEventSubscription[] subscriptions = services
            .Where(descriptor => descriptor.ServiceType == typeof(IntegrationEventSubscription))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<IntegrationEventSubscription>()
            .ToArray();
        Assert.Equal(2, subscriptions.Length);
        Assert.All(subscriptions, subscription => Assert.True(subscription.IsTenantScoped()));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAccessProfileAssignmentPolicy) &&
            descriptor.ImplementationType == typeof(WorkspaceAccessProfileAssignmentPolicy));
    }

    [Fact]
    public async Task Active_owner_receives_only_the_tenant_scoped_owner_assignment()
    {
        FakeAccessControlRoleProvisioner accessControl = new();
        RecordingProfileAssignmentRevoker profileAssignments = new(accessControl);
        OrganizationMembershipAccessHandler handler = new(accessControl, profileAssignments);
        OrganizationMembershipChangedIntegrationEvent integrationEvent = CreateEvent(
            OrganizationMembershipRole.Owner,
            OrganizationMembershipStatus.Active);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        string scope = $"tenant:{integrationEvent.ScopeId}";
        Assert.Contains((integrationEvent.SubjectId, WorkspaceAccessRoles.Owner, scope), accessControl.Assignments);
        Assert.DoesNotContain((integrationEvent.SubjectId, WorkspaceAccessRoles.MembershipMarker, scope), accessControl.Assignments);
        Assert.DoesNotContain((integrationEvent.SubjectId, WorkspaceAccessRoles.LegacyMember, scope), accessControl.Assignments);
        Assert.Equal(WorkspaceAccessRoles.OwnerPermissions, accessControl.Permissions[WorkspaceAccessRoles.Owner]);
        Assert.Empty(accessControl.Permissions[WorkspaceAccessRoles.MembershipMarker]);
        Assert.Empty(profileAssignments.Calls);
    }

    [Fact]
    public async Task Active_member_membership_does_not_grant_operational_access()
    {
        FakeAccessControlRoleProvisioner accessControl = new();
        OrganizationMembershipAccessHandler handler = new(
            accessControl,
            new RecordingProfileAssignmentRevoker(accessControl));
        OrganizationMembershipChangedIntegrationEvent integrationEvent = CreateEvent(
            OrganizationMembershipRole.Member,
            OrganizationMembershipStatus.Active);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.DoesNotContain(accessControl.Assignments, assignment =>
            assignment.SubjectId == integrationEvent.SubjectId);
    }

    [Fact]
    public async Task Inactive_membership_removes_both_workspace_assignments()
    {
        OrganizationMembershipChangedIntegrationEvent integrationEvent = CreateEvent(
            OrganizationMembershipRole.Member,
            OrganizationMembershipStatus.Suspended);
        string scope = $"tenant:{integrationEvent.ScopeId}";
        FakeAccessControlRoleProvisioner accessControl = new();
        accessControl.Assignments.Add((integrationEvent.SubjectId, WorkspaceAccessRoles.Owner, scope));
        accessControl.Assignments.Add((integrationEvent.SubjectId, WorkspaceAccessRoles.MembershipMarker, scope));
        accessControl.Assignments.Add((integrationEvent.SubjectId, WorkspaceAccessRoles.LegacyMember, scope));
        RecordingProfileAssignmentRevoker profileAssignments = new(accessControl);
        OrganizationMembershipAccessHandler handler = new(accessControl, profileAssignments);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.DoesNotContain(accessControl.Assignments, assignment => assignment.SubjectId == integrationEvent.SubjectId);
        ProfileRevocationCall revocation = Assert.Single(profileAssignments.Calls);
        Assert.True(revocation.CompatibilityAssignmentsWereRemoved);
        Assert.Equal(AccessSubject.User(integrationEvent.SubjectId), revocation.Subject);
        Assert.Equal(AccessScope.Parse(scope), revocation.Scope);
        Assert.Equal(AccessSubject.System(OrganizationMembershipAccessHandler.RevocationActorId), revocation.Actor);
    }

    [Fact]
    public void Member_role_is_a_front_desk_baseline_without_administration_permissions()
    {
        Assert.Contains(ReservationsAdminPermissionCodes.Create, WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.Contains(ReservationsAdminPermissionCodes.CheckIn, WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.Contains(GuestsAdminPermissionCodes.Manage, WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.Contains(InventoryAdminPermissionCodes.BlocksManage, WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.Contains(StaffAdminPermissionCodes.Read, WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.DoesNotContain("properties.properties.manage", WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.DoesNotContain(StaffAdminPermissionCodes.Manage, WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.DoesNotContain("ingestion.connections.manage", WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.Empty(WorkspaceAccessRoles.MembershipMarkerPermissions);
    }

    [Fact]
    public void Custom_profiles_are_limited_to_the_explicit_front_desk_allowlist()
    {
        Assert.All(WorkspaceAccessRoles.LegacyMemberPermissions, permission =>
            Assert.Contains(permission, WorkspaceAccessRoles.DelegablePermissions));
        Assert.All(WorkspaceAccessProfileSeeds.Manager.Permissions, permission =>
            Assert.Contains(permission, WorkspaceAccessRoles.DelegablePermissions));
        Assert.Equal(
            WorkspaceAccessRoles.DelegablePermissions.Count,
            WorkspaceAccessRoles.DelegablePermissions.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(WorkspaceAccessRoles.OwnerPermissions, permission =>
            WorkspaceAccessRoles.DelegablePermissions.Contains(permission, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Demoting_the_final_owner_surfaces_access_control_protection()
    {
        OrganizationMembershipChangedIntegrationEvent integrationEvent = CreateEvent(
            OrganizationMembershipRole.Member,
            OrganizationMembershipStatus.Active);
        FakeAccessControlRoleProvisioner accessControl = new()
        {
            ProtectedRole = WorkspaceAccessRoles.Owner
        };
        OrganizationMembershipAccessHandler handler = new(
            accessControl,
            new RecordingProfileAssignmentRevoker(accessControl));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(integrationEvent, CancellationToken.None));

        Assert.Contains("final owner", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static OrganizationMembershipChangedIntegrationEvent CreateEvent(
        OrganizationMembershipRole role,
        OrganizationMembershipStatus status)
    {
        Guid organizationId = Guid.NewGuid();
        return new(
            Guid.NewGuid(),
            TestClock.Now,
            organizationId.ToString("D"),
            organizationId,
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            OrganizationMembershipChange.Joined,
            role,
            status,
            1);
    }

    private static class TestClock
    {
        public static DateTimeOffset Now { get; } = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeAccessControlRoleProvisioner : IAccessControlRoleProvisioner
    {
        public Dictionary<string, List<string>> Permissions { get; } = new(StringComparer.Ordinal);
        public HashSet<(string SubjectId, string Role, string Scope)> Assignments { get; } = [];
        public string? ProtectedRole { get; init; }

        public Task EnsureRoleAsync(AccessControlRoleDefinition role,
            CancellationToken cancellationToken)
        {
            this.Permissions[role.Name] = role.Permissions.ToList();
            return Task.CompletedTask;
        }

        public Task EnsureAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken)
        {
            this.Assignments.Add((subject.Id, roleName, scope.Value));
            return Task.CompletedTask;
        }

        public Task<AccessControlAssignmentRemovalOutcome> RemoveAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken)
        {
            if (string.Equals(roleName, this.ProtectedRole, StringComparison.Ordinal))
            {
                return Task.FromResult(AccessControlAssignmentRemovalOutcome.LastOwnerProtected);
            }

            return Task.FromResult(this.Assignments.Remove((subject.Id, roleName, scope.Value))
                ? AccessControlAssignmentRemovalOutcome.Removed
                : AccessControlAssignmentRemovalOutcome.NotFound);
        }

        public Task<bool> HasAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Assignments.Contains((subject.Id, roleName, scope.Value)));

        public Task<AccessControlPage<AccessControlRoleAssignment>> ListAssignmentsAsync(
            string roleName,
            AccessScope scope,
            int page,
            int pageSize,
            CancellationToken cancellationToken) => Task.FromResult(
            new AccessControlPage<AccessControlRoleAssignment>([], page, pageSize, false));
    }

    private sealed class RecordingProfileAssignmentRevoker(
        FakeAccessControlRoleProvisioner accessControl) : IAccessProfileAssignmentRevoker
    {
        public List<ProfileRevocationCall> Calls { get; } = [];

        public Task<int> RevokeAllAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            AccessSubject actor,
            CancellationToken cancellationToken)
        {
            bool compatibilityAssignmentsWereRemoved = !accessControl.Assignments.Any(assignment =>
                assignment.SubjectId == subject.Id &&
                assignment.Scope == ownerScope.Value);
            this.Calls.Add(new ProfileRevocationCall(
                subject,
                ownerScope,
                actor,
                compatibilityAssignmentsWereRemoved));
            return Task.FromResult(0);
        }
    }

    private sealed record ProfileRevocationCall(
        AccessSubject Subject,
        AccessScope Scope,
        AccessSubject Actor,
        bool CompatibilityAssignmentsWereRemoved);
}
