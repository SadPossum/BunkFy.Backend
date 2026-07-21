namespace Integration.Tests;

using System.Security.Claims;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Host.Api;
using Gma.Framework.AccessControl;
using Gma.Framework.Scoping;
using Gma.Framework.Security;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceNotificationUserScopeAuthorizerTests
{
    [Fact]
    public async Task Matching_token_scope_does_not_replace_current_workspace_assignment()
    {
        StubRoleProvisioner accessControl = new();
        WorkspaceNotificationUserScopeAuthorizer authorizer = new(accessControl);
        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [new Claim(ApplicationClaimNames.ScopeId, "tenant-a")],
            authenticationType: "test"));

        bool authorized = await authorizer.AuthorizeAsync(
            principal,
            AccessSubject.User("user-a"),
            new TestScopeContext("tenant-a"),
            CancellationToken.None);

        Assert.False(authorized);
        Assert.Equal(
            [WorkspaceAccessRoles.Owner, WorkspaceAccessRoles.MembershipMarker, WorkspaceAccessRoles.LegacyMember],
            accessControl.CheckedRoles);
    }

    [Theory]
    [InlineData(WorkspaceAccessRoles.Owner)]
    [InlineData(WorkspaceAccessRoles.MembershipMarker)]
    [InlineData(WorkspaceAccessRoles.LegacyMember)]
    public async Task Current_workspace_owner_or_member_assignment_authorizes_notifications(string roleName)
    {
        StubRoleProvisioner accessControl = new(roleName);
        WorkspaceNotificationUserScopeAuthorizer authorizer = new(accessControl);

        bool authorized = await authorizer.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            AccessSubject.User("user-a"),
            new TestScopeContext("tenant-a"),
            CancellationToken.None);

        Assert.True(authorized);
    }

    private sealed class StubRoleProvisioner(params string[] assignedRoles) : IAccessControlRoleProvisioner
    {
        private readonly HashSet<string> assigned = assignedRoles.ToHashSet(StringComparer.Ordinal);

        public List<string> CheckedRoles { get; } = [];

        public Task<bool> HasAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.CheckedRoles.Add(roleName);
            return Task.FromResult(this.assigned.Contains(roleName));
        }

        public Task EnsureRoleAsync(
            AccessControlRoleDefinition role,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task EnsureAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AccessControlAssignmentRemovalOutcome> RemoveAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AccessControlPage<AccessControlRoleAssignment>> ListAssignmentsAsync(
            string roleName,
            AccessScope scope,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => scopeId;
    }
}
