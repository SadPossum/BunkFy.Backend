namespace BunkFy.Extensions.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceAccessProfileAssignmentPolicyTests
{
    private static readonly AccessScope WorkspaceScope = AccessScope.Parse("tenant:workspace-a");
    private static readonly AccessSubject Actor = AccessSubject.User("owner-a");
    private static readonly AccessSubject Target = AccessSubject.User("member-a");

    [Theory]
    [InlineData(WorkspaceAccessRoles.Owner)]
    [InlineData(WorkspaceAccessRoles.MembershipMarker)]
    [InlineData(WorkspaceAccessRoles.LegacyMember)]
    public async Task Active_workspace_compatibility_assignment_allows_profile_target(string roleName)
    {
        StubRoleProvisioner accessControl = new(WorkspaceScope, roleName);
        WorkspaceAccessProfileAssignmentPolicy policy = new(accessControl);

        bool allowed = await policy.IsAllowedAsync(CreateContext(Target), CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact]
    public async Task Subject_without_workspace_compatibility_assignment_is_rejected()
    {
        StubRoleProvisioner accessControl = new(WorkspaceScope);
        WorkspaceAccessProfileAssignmentPolicy policy = new(accessControl);

        bool allowed = await policy.IsAllowedAsync(CreateContext(Target), CancellationToken.None);

        Assert.False(allowed);
        Assert.Equal(3, accessControl.CheckedRoles.Count);
    }

    [Fact]
    public async Task Compatibility_assignment_in_another_workspace_is_rejected()
    {
        StubRoleProvisioner accessControl = new(
            AccessScope.Parse("tenant:workspace-b"),
            WorkspaceAccessRoles.MembershipMarker);
        WorkspaceAccessProfileAssignmentPolicy policy = new(accessControl);

        bool allowed = await policy.IsAllowedAsync(CreateContext(Target), CancellationToken.None);

        Assert.False(allowed);
        Assert.All(accessControl.CheckedScopes, scope => Assert.Equal(WorkspaceScope, scope));
    }

    [Fact]
    public async Task Non_user_and_non_workspace_scope_targets_are_rejected_without_lookup()
    {
        StubRoleProvisioner accessControl = new(WorkspaceScope, WorkspaceAccessRoles.MembershipMarker);
        WorkspaceAccessProfileAssignmentPolicy policy = new(accessControl);

        bool serviceAllowed = await policy.IsAllowedAsync(
            CreateContext(AccessSubject.Service("service-a")),
            CancellationToken.None);
        bool nestedScopeAllowed = await policy.IsAllowedAsync(
            CreateContext(
                Target,
                AccessScope.Parse("tenant:workspace-a/property:property-a")),
            CancellationToken.None);

        Assert.False(serviceAllowed);
        Assert.False(nestedScopeAllowed);
        Assert.Empty(accessControl.CheckedRoles);
    }

    private static AccessProfileAssignmentPolicyContext CreateContext(
        AccessSubject subject,
        AccessScope? scope = null) =>
        new(
            Guid.NewGuid(),
            "front-desk",
            scope ?? WorkspaceScope,
            Actor,
            subject,
            ["reservations.read"]);

    private sealed class StubRoleProvisioner(AccessScope assignedScope, params string[] assignedRoles)
        : IAccessControlRoleProvisioner
    {
        private readonly HashSet<(string Role, string Scope)> assigned = assignedRoles
            .Select(role => (role, assignedScope.Value))
            .ToHashSet();

        public List<string> CheckedRoles { get; } = [];
        public List<AccessScope> CheckedScopes { get; } = [];

        public Task<bool> HasAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default)
        {
            this.CheckedRoles.Add(roleName);
            this.CheckedScopes.Add(scope);
            return Task.FromResult(this.assigned.Contains((roleName, scope.Value)));
        }

        public Task EnsureRoleAsync(
            AccessControlRoleDefinition role,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EnsureAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AccessControlAssignmentRemovalOutcome> RemoveAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AccessControlAssignmentRemovalOutcome.NotFound);

        public Task<AccessControlPage<AccessControlRoleAssignment>> ListAssignmentsAsync(
            string roleName,
            AccessScope scope,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AccessControlPage<AccessControlRoleAssignment>([], page, pageSize, false));
    }
}
