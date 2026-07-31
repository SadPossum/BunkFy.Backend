namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Workspaces.Contracts;

internal sealed class WorkspaceIngestionTenantLifecyclePolicy(
    WorkspaceOperationalAdmissionEvaluator admission)
    : IIngestionTenantLifecyclePolicy
{
    public async ValueTask<IngestionTenantLifecycleDecision> AuthorizeAsync(
        string tenantId,
        IngestionTenantLifecycleOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == IngestionTenantLifecycleOperation.Unknown)
        {
            return IngestionTenantLifecycleDecision.Restricted;
        }

        WorkspaceOperationalAdmissionDecision decision =
            await admission.EvaluateAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
        return decision.Outcome switch
        {
            WorkspaceOperationalAdmissionOutcome.Allowed =>
                IngestionTenantLifecycleDecision.Allowed,
            WorkspaceOperationalAdmissionOutcome.Restricted =>
                IngestionTenantLifecycleDecision.Restricted,
            _ => IngestionTenantLifecycleDecision.Unavailable
        };
    }
}
