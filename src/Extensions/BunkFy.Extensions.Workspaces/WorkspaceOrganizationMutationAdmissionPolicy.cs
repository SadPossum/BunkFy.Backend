namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.Organizations.Contracts;

internal sealed class WorkspaceOrganizationMutationAdmissionPolicy(
    IWorkspaceOperationalAdmissionPolicy operationalAdmission)
    : IOrganizationMutationAdmissionPolicy
{
    public async ValueTask<OrganizationMutationAdmissionDecision> EvaluateAsync(
        OrganizationMutationAdmissionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        WorkspaceOperationalAdmissionDecision decision =
            await operationalAdmission.EvaluateAsync(
                context.OrganizationId.ToString("D"),
                cancellationToken).ConfigureAwait(false);
        return decision.Outcome switch
        {
            WorkspaceOperationalAdmissionOutcome.Allowed =>
                OrganizationMutationAdmissionDecision.Allowed,
            WorkspaceOperationalAdmissionOutcome.Restricted =>
                OrganizationMutationAdmissionDecision.Denied,
            _ => OrganizationMutationAdmissionDecision.Unavailable
        };
    }
}
