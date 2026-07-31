namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.AccessControl.Contracts;

internal sealed class WorkspaceAccessProfileMutationAdmissionPolicy(
    IWorkspaceOperationalAdmissionPolicy operationalAdmission)
    : IAccessProfileMutationAdmissionPolicy
{
    public async ValueTask<AccessProfileMutationAdmissionDecision>
        EvaluateAsync(
            AccessProfileMutationAdmissionContext context,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!WorkspaceAccessScopes.IsWorkspaceOrPropertyScope(
                context.OwnerScope))
        {
            return AccessProfileMutationAdmissionDecision.Allowed;
        }

        WorkspaceOperationalAdmissionDecision decision =
            await operationalAdmission.EvaluateAsync(
                context.OwnerScope.Segments[0].Value,
                cancellationToken).ConfigureAwait(false);
        return decision.Outcome switch
        {
            WorkspaceOperationalAdmissionOutcome.Allowed =>
                AccessProfileMutationAdmissionDecision.Allowed,
            WorkspaceOperationalAdmissionOutcome.Restricted =>
                AccessProfileMutationAdmissionDecision.Denied,
            _ => AccessProfileMutationAdmissionDecision.Unavailable
        };
    }
}
