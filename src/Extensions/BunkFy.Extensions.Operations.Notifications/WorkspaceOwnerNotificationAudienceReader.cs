namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Extensions.Workspaces;
using Gma.Framework.AccessControl;
using Gma.Framework.Naming;
using Gma.Modules.AccessControl.Contracts;

internal sealed class WorkspaceOwnerNotificationAudienceReader(
    IAccessControlRoleProvisioner accessControl) : IWorkspaceOwnerNotificationAudienceReader
{
    private const int AssignmentPageSize = 100;

    public async Task<IReadOnlyList<string>> ListAuthSubjectIdsAsync(
        string scopeId,
        CancellationToken cancellationToken)
    {
        string normalizedScopeId = ScopeIds.Normalize(scopeId);
        AccessScope workspaceScope = AccessScope.Create(
            AccessScopeSegment.Create("tenant", normalizedScopeId));

        HashSet<string> subjectIds = new(StringComparer.Ordinal);
        int page = 1;
        while (true)
        {
            AccessControlPage<AccessControlRoleAssignment> assignments = await accessControl
                .ListAssignmentsAsync(
                    WorkspaceAccessRoles.Owner,
                    workspaceScope,
                    page,
                    AssignmentPageSize,
                    cancellationToken)
                .ConfigureAwait(false);
            foreach (AccessControlRoleAssignment assignment in assignments.Items)
            {
                if (assignment.SubjectKind == AccessSubjectKind.User)
                {
                    subjectIds.Add(assignment.SubjectId);
                }
            }

            if (!assignments.HasMore)
            {
                return subjectIds.Order(StringComparer.Ordinal).ToArray();
            }

            page = checked(page + 1);
        }
    }
}
