namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Contracts;

internal sealed class WorkspacePropertyProcessingLifecyclePolicy(
    WorkspaceOperationalAdmissionEvaluator admission)
    : IPropertyProcessingLifecyclePolicy
{
    public async ValueTask<PropertyProcessingLifecycleDecision>
        AuthorizeActivationAsync(
            string tenantId,
            Guid propertyId,
            CancellationToken cancellationToken = default)
    {
        if (propertyId == Guid.Empty)
        {
            return PropertyProcessingLifecycleDecision.Restricted;
        }

        WorkspaceOperationalAdmissionDecision decision =
            await admission.EvaluateAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
        return decision.Outcome switch
        {
            WorkspaceOperationalAdmissionOutcome.Allowed =>
                PropertyProcessingLifecycleDecision.Allowed,
            WorkspaceOperationalAdmissionOutcome.Restricted =>
                PropertyProcessingLifecycleDecision.Restricted,
            _ => PropertyProcessingLifecycleDecision.Unavailable
        };
    }
}
