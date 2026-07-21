namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;

internal sealed class WorkspaceAccessProfileAssignmentPolicy(
    IAccessControlRoleProvisioner accessControl) : IAccessProfileAssignmentPolicy
{
    public async ValueTask<bool> IsAllowedAsync(
        AccessProfileAssignmentPolicyContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Subject.Kind != AccessSubjectKind.User ||
            !WorkspaceAccessScopes.IsWorkspaceScope(context.OwnerScope))
        {
            return false;
        }

        if (await accessControl.HasAssignmentAsync(
                context.Subject,
                WorkspaceAccessRoles.Owner,
                context.OwnerScope,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        return await accessControl.HasAssignmentAsync(
                context.Subject,
                WorkspaceAccessRoles.Member,
                context.OwnerScope,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
