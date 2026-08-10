namespace Integration.Tests;

using System.Security.Claims;
using BunkFy.Extensions.Workspaces;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Host.Api;
using Gma.Framework.AccessControl;
using Gma.Framework.Scoping;
using Gma.Framework.Security;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Auth.Contracts;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceNotificationUserScopeAuthorizerTests
{
    [Fact]
    public async Task Matching_token_scope_does_not_replace_current_workspace_assignment()
    {
        StubRoleProvisioner accessControl = new();
        Guid memberId = Guid.NewGuid();
        WorkspaceNotificationUserScopeAuthorizer authorizer = CreateAuthorizer(
            accessControl,
            new StubAdmissionReader(new AuthMemberAdmission(null)));
        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [new Claim(ApplicationClaimNames.ScopeId, "tenant-a")],
            authenticationType: "test"));

        bool authorized = await authorizer.AuthorizeAsync(
            principal,
            AccessSubject.User(memberId.ToString("D")),
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
        WorkspaceNotificationUserScopeAuthorizer authorizer = CreateAuthorizer(
            accessControl,
            new StubAdmissionReader(new AuthMemberAdmission(null)));

        bool authorized = await authorizer.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            AccessSubject.User(Guid.NewGuid().ToString("D")),
            new TestScopeContext("tenant-a"),
            CancellationToken.None);

        Assert.True(authorized);
    }

    [Fact]
    public async Task Disabled_auth_member_is_denied_before_workspace_access_lookup()
    {
        StubRoleProvisioner accessControl = new(WorkspaceAccessRoles.Owner);
        var admissions = new StubAdmissionReader(null);
        WorkspaceNotificationUserScopeAuthorizer authorizer = CreateAuthorizer(
            accessControl,
            admissions);

        bool authorized = await authorizer.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            AccessSubject.User(Guid.NewGuid().ToString("D")),
            new TestScopeContext("tenant-a"),
            CancellationToken.None);

        Assert.False(authorized);
        Assert.Equal(1, admissions.CallCount);
        Assert.Empty(accessControl.CheckedRoles);
    }

    private static WorkspaceNotificationUserScopeAuthorizer CreateAuthorizer(
        IAccessControlRoleProvisioner accessControl,
        IAuthMemberAdmissionReader admissions) =>
        new(
            accessControl,
            admissions,
            Options.Create(new BunkFyWorkspacesOptions
            {
                GlobalAuthScopeId = "bunkfy-auth"
            }));

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

    private sealed class StubAdmissionReader(AuthMemberAdmission? admission)
        : IAuthMemberAdmissionReader
    {
        public int CallCount { get; private set; }

        public ValueTask<AuthMemberAdmission?> FindActiveAsync(
            string scopeId,
            Guid memberId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.CallCount++;
            Assert.Equal("bunkfy-auth", scopeId);
            return ValueTask.FromResult(admission);
        }
    }
}
