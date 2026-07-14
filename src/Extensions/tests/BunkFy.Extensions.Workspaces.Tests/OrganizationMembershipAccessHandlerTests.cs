namespace BunkFy.Extensions.Workspaces.Tests;

using Gma.Framework.AccessControl;
using Gma.Framework.Permissions;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Messaging;
using Gma.Modules.AccessControl.Application;
using Gma.Modules.AccessControl.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Framework.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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
    }

    [Fact]
    public async Task Active_owner_receives_only_the_tenant_scoped_owner_assignment()
    {
        FakeAccessControlRepository accessControl = new();
        OrganizationMembershipAccessHandler handler = new(accessControl, new TestClock());
        OrganizationMembershipChangedIntegrationEvent integrationEvent = CreateEvent(
            OrganizationMembershipRole.Owner,
            OrganizationMembershipStatus.Active);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        string scope = $"tenant:{integrationEvent.ScopeId}";
        Assert.Contains((integrationEvent.SubjectId, WorkspaceAccessRoles.Owner, scope), accessControl.Assignments);
        Assert.DoesNotContain((integrationEvent.SubjectId, WorkspaceAccessRoles.Member, scope), accessControl.Assignments);
        Assert.Equal(WorkspaceAccessRoles.OwnerPermissions, accessControl.Permissions[WorkspaceAccessRoles.Owner]);
        Assert.Equal(WorkspaceAccessRoles.MemberPermissions, accessControl.Permissions[WorkspaceAccessRoles.Member]);
    }

    [Fact]
    public async Task Inactive_membership_removes_both_workspace_assignments()
    {
        OrganizationMembershipChangedIntegrationEvent integrationEvent = CreateEvent(
            OrganizationMembershipRole.Member,
            OrganizationMembershipStatus.Suspended);
        string scope = $"tenant:{integrationEvent.ScopeId}";
        FakeAccessControlRepository accessControl = new();
        accessControl.Assignments.Add((integrationEvent.SubjectId, WorkspaceAccessRoles.Owner, scope));
        accessControl.Assignments.Add((integrationEvent.SubjectId, WorkspaceAccessRoles.Member, scope));
        OrganizationMembershipAccessHandler handler = new(accessControl, new TestClock());

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.DoesNotContain(accessControl.Assignments, assignment => assignment.SubjectId == integrationEvent.SubjectId);
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

    private sealed class TestClock : ISystemClock
    {
        public static DateTimeOffset Now { get; } = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FakeAccessControlRepository : IAccessControlRbacRepository
    {
        public Dictionary<string, List<string>> Permissions { get; } = new(StringComparer.Ordinal);
        public HashSet<(string SubjectId, string Role, string Scope)> Assignments { get; } = [];

        public Task EnsureSubjectAsync(AccessSubject subject, DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task EnsureRoleAsync(string roleName, DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken)
        {
            this.Permissions.TryAdd(roleName, []);
            return Task.CompletedTask;
        }

        public Task EnsureRolePermissionAsync(string roleName, string permissionCode,
            DateTimeOffset createdAtUtc, CancellationToken cancellationToken)
        {
            List<string> permissions = this.Permissions[roleName];
            if (!permissions.Contains(permissionCode, StringComparer.Ordinal))
            {
                permissions.Add(permissionCode);
            }

            return Task.CompletedTask;
        }

        public Task EnsureRoleAssignmentAsync(AccessSubject subject, string roleName, AccessScope scope,
            DateTimeOffset createdAtUtc, CancellationToken cancellationToken)
        {
            this.Assignments.Add((subject.Id, roleName, scope.Value));
            return Task.CompletedTask;
        }

        public Task<AccessControlRemovalOutcome> UnassignRoleAsync(AccessSubject subject, string roleName,
            AccessScope scope, CancellationToken cancellationToken) => Task.FromResult(
            this.Assignments.Remove((subject.Id, roleName, scope.Value))
                ? AccessControlRemovalOutcome.Removed
                : AccessControlRemovalOutcome.NotFound);

        public Task<bool> HasAnyAssignmentsAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> TryBootstrapOwnerAsync(AccessSubject subject, string roleName,
            DateTimeOffset createdAtUtc, bool allowWhenAssignmentsExist,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken) =>
            Task.FromResult(this.Permissions.ContainsKey(roleName));
        public Task<bool> RoleHasPermissionAsync(string roleName, string permissionCode,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Permissions.TryGetValue(roleName, out List<string>? values) && values.Contains(permissionCode));
        public Task<bool> AssignmentExistsAsync(AccessSubject subject, string roleName, AccessScope scope,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Assignments.Contains((subject.Id, roleName, scope.Value)));
        public Task<bool> HasPermissionAsync(AccessSubject subject, PermissionCode permission, AccessScope scope,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<AccessGrantScope>> ListGrantedScopesAsync(AccessSubject subject,
            PermissionCode permission, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AccessGrantScope>>([]);
        public Task<AccessControlRoleDetails> CreateRoleAsync(string roleName, DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GrantRolePermissionAsync(string roleName, string permissionCode,
            DateTimeOffset createdAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AssignRoleAsync(AccessSubject subject, string roleName, AccessScope scope,
            DateTimeOffset createdAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AccessControlRemovalOutcome> RevokeRolePermissionAsync(string roleName,
            string permissionCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccessControlRoleDetails>> ListRolesAsync(
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AccessControlRoleDetails>>([]);
        public Task<IReadOnlyList<AccessControlRoleAssignmentDetails>> ListRoleAssignmentsAsync(
            string roleName, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AccessControlRoleAssignmentDetails>>([]);
    }
}
