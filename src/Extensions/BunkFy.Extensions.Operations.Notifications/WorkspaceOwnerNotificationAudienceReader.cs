namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Extensions.Workspaces;
using Gma.Framework.AccessControl;
using Gma.Framework.Naming;
using Gma.Modules.AccessControl.Application.Ports;

internal sealed class WorkspaceOwnerNotificationAudienceReader(
    IAccessControlRbacRepository accessControl) : IWorkspaceOwnerNotificationAudienceReader
{
    public async Task<IReadOnlyList<string>> ListAuthSubjectIdsAsync(
        string scopeId,
        CancellationToken cancellationToken)
    {
        string normalizedScopeId = ScopeIds.Normalize(scopeId);
        AccessScope workspaceScope = AccessScope.Create(
            AccessScopeSegment.Create("tenant", normalizedScopeId));

        return (await accessControl
                .ListRoleAssignmentsAsync(
                    WorkspaceAccessRoles.Owner,
                    workspaceScope,
                    cancellationToken)
                .ConfigureAwait(false))
            .Where(assignment => assignment.SubjectKind == AccessSubjectKind.User)
            .Select(assignment => assignment.SubjectId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
