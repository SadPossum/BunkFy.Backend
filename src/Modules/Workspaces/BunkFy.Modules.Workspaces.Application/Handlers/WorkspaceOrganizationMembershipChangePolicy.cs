namespace BunkFy.Modules.Workspaces.Application.Handlers;

using Gma.Modules.Organizations.Contracts;

internal sealed class WorkspaceOrganizationMembershipChangePolicy
    : IOrganizationMembershipChangePolicy
{
    public ValueTask<OrganizationMembershipChangePolicyDecision> EvaluateAsync(
        OrganizationMembershipChangePolicyRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OrganizationMembershipChangePolicyDecision.Denied);
}
