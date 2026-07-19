namespace BunkFy.Extensions.Operations.Notifications.Tests;

using BunkFy.Extensions.Workspaces;
using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceOwnerNotificationAudienceReaderTests
{
    [Fact]
    public async Task Owner_audience_is_paged_bounded_distinct_and_user_only()
    {
        const string scopeId = "tenant-a";
        AccessScope scope = AccessScope.Create(AccessScopeSegment.Create("tenant", scopeId));
        FakeAccessControlRoleProvisioner accessControl = new(
        [
            Page(1, true,
                Assignment(AccessSubjectKind.User, "user-b", scope),
                Assignment(AccessSubjectKind.Service, "service-a", scope)),
            Page(2, false,
                Assignment(AccessSubjectKind.User, "user-a", scope),
                Assignment(AccessSubjectKind.User, "user-b", scope))
        ]);
        WorkspaceOwnerNotificationAudienceReader reader = new(accessControl);

        IReadOnlyList<string> audience = await reader.ListAuthSubjectIdsAsync(
            scopeId,
            CancellationToken.None);

        Assert.Equal(["user-a", "user-b"], audience);
        Assert.Equal([1, 2], accessControl.RequestedPages);
        Assert.All(accessControl.RequestedPageSizes, size => Assert.Equal(100, size));
        Assert.All(accessControl.RequestedRoles, role => Assert.Equal(WorkspaceAccessRoles.Owner, role));
        Assert.All(accessControl.RequestedScopes, requested => Assert.Equal(scope, requested));
    }

    private static AccessControlRoleAssignment Assignment(
        AccessSubjectKind kind,
        string subjectId,
        AccessScope scope) => new(
        Guid.NewGuid(),
        kind,
        subjectId,
        WorkspaceAccessRoles.Owner,
        scope,
        DateTimeOffset.UtcNow);

    private static AccessControlPage<AccessControlRoleAssignment> Page(
        int page,
        bool hasMore,
        params AccessControlRoleAssignment[] assignments) =>
        new(assignments, page, 100, hasMore);

    private sealed class FakeAccessControlRoleProvisioner(
        IReadOnlyList<AccessControlPage<AccessControlRoleAssignment>> pages)
        : IAccessControlRoleProvisioner
    {
        public List<int> RequestedPages { get; } = [];
        public List<int> RequestedPageSizes { get; } = [];
        public List<string> RequestedRoles { get; } = [];
        public List<AccessScope> RequestedScopes { get; } = [];

        public Task<AccessControlPage<AccessControlRoleAssignment>> ListAssignmentsAsync(
            string roleName,
            AccessScope scope,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            this.RequestedPages.Add(page);
            this.RequestedPageSizes.Add(pageSize);
            this.RequestedRoles.Add(roleName);
            this.RequestedScopes.Add(scope);
            return Task.FromResult(pages[page - 1]);
        }

        public Task EnsureRoleAsync(
            AccessControlRoleDefinition role,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task EnsureAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AccessControlAssignmentRemovalOutcome> RemoveAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> HasAssignmentAsync(
            AccessSubject subject,
            string roleName,
            AccessScope scope,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
