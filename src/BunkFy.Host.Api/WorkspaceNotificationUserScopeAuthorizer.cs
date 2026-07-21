namespace BunkFy.Host.Api;

using System.Security.Claims;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Notifications.Api;

internal sealed class WorkspaceNotificationUserScopeAuthorizer(
    IAccessControlRoleProvisioner accessControl)
    : INotificationUserScopeAuthorizer
{
    public async Task<bool> AuthorizeAsync(
        ClaimsPrincipal principal,
        AccessSubject subject,
        IScopeContext scopeContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(scopeContext);

        if (!scopeContext.IsEnabled)
        {
            return true;
        }

        if (subject.Kind != AccessSubjectKind.User ||
            !ScopeIds.TryNormalize(scopeContext.ScopeId, out string? workspaceId))
        {
            return false;
        }

        AccessScope workspaceScope = AccessScope.Create(
            AccessScopeSegment.Create("tenant", workspaceId));
        return await accessControl.HasAssignmentAsync(
                subject,
                WorkspaceAccessRoles.Owner,
                workspaceScope,
                cancellationToken).ConfigureAwait(false) ||
            await accessControl.HasAssignmentAsync(
                subject,
                WorkspaceAccessRoles.MembershipMarker,
                workspaceScope,
                cancellationToken).ConfigureAwait(false) ||
            await accessControl.HasAssignmentAsync(
                subject,
                WorkspaceAccessRoles.LegacyMember,
                workspaceScope,
                cancellationToken).ConfigureAwait(false);
    }
}
