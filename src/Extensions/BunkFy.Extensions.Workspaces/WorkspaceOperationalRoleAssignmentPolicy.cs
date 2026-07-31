namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.AccessControl.Contracts;

internal sealed class WorkspaceOperationalRoleAssignmentPolicy(
    IWorkspaceOperationalAdmissionPolicy operationalAdmission)
    : IAccessRoleAssignmentPolicy
{
    public async ValueTask<bool> IsAllowedAsync(
        AccessRoleAssignmentPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!WorkspaceAccessScopes.IsWorkspaceOrPropertyScope(context.AccessScope))
        {
            return true;
        }

        WorkspaceOperationalAdmissionDecision decision =
            await operationalAdmission.EvaluateAsync(
                context.AccessScope.Segments[0].Value,
                cancellationToken).ConfigureAwait(false);
        return decision.Outcome == WorkspaceOperationalAdmissionOutcome.Allowed;
    }
}
