namespace BunkFy.Host.Api;

using System.Security.Claims;
using BunkFy.Extensions.Workspaces;
using Gma.Framework.AccessControl;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Gma.Framework.Security;
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

        string? tokenScopeId = principal.FindFirstValue(ApplicationClaimNames.ScopeId);
        if (ScopeIds.TryNormalize(tokenScopeId, out string? normalizedTokenScopeId) &&
            string.Equals(normalizedTokenScopeId, scopeContext.ScopeId, StringComparison.Ordinal))
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
                WorkspaceAccessRoles.Member,
                workspaceScope,
                cancellationToken).ConfigureAwait(false);
    }
}
