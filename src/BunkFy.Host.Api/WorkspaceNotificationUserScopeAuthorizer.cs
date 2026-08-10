namespace BunkFy.Host.Api;

using System.Security.Claims;
using BunkFy.Extensions.Workspaces;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Notifications.Api;
using Microsoft.Extensions.Options;

internal sealed class WorkspaceNotificationUserScopeAuthorizer(
    IAccessControlRoleProvisioner accessControl,
    IAuthMemberAdmissionReader admissions,
    IOptions<BunkFyWorkspacesOptions> workspaceOptions)
    : INotificationUserScopeAuthorizer
{
    private static readonly IReadOnlyCollection<string> WorkspaceMembershipRoles =
    [
        WorkspaceAccessRoles.Owner,
        WorkspaceAccessRoles.MembershipMarker,
        WorkspaceAccessRoles.LegacyMember
    ];

    public async Task<bool> AuthorizeAsync(
        ClaimsPrincipal principal,
        AccessSubject subject,
        IScopeContext scopeContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(scopeContext);

        if (subject.Kind != AccessSubjectKind.User ||
            !Guid.TryParse(subject.Id, out Guid memberId))
        {
            return false;
        }

        AuthMemberAdmission? admission = await admissions.FindActiveAsync(
                workspaceOptions.Value.GlobalAuthScopeId,
                memberId,
                cancellationToken)
            .ConfigureAwait(false);
        if (admission is null)
        {
            return false;
        }

        if (!scopeContext.IsEnabled)
        {
            return true;
        }

        if (!ScopeIds.TryNormalize(scopeContext.ScopeId, out string? workspaceId))
        {
            return false;
        }

        AccessScope workspaceScope = AccessScope.Create(
            AccessScopeSegment.Create("tenant", workspaceId));
        return await accessControl.HasAnyAssignmentAsync(
                subject,
                WorkspaceMembershipRoles,
                workspaceScope,
                cancellationToken).ConfigureAwait(false);
    }
}
