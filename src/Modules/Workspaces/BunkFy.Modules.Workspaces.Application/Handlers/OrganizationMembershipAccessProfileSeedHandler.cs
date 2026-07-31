namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;

[IntegrationEventHandler(HandlerName, RequiresExplicitProducerBinding = true)]
internal sealed class OrganizationMembershipAccessProfileSeedHandler(
    WorkspaceAccessProvisioner provisioner,
    WorkspaceOperationalAdmissionEvaluator operationalAdmission)
    : IIntegrationEventHandler<OrganizationMembershipChangedIntegrationEvent>
{
    public const string HandlerName = WorkspacesModuleMetadata.MembershipAccessSeedHandlerName;

    public async Task HandleAsync(
        OrganizationMembershipChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.Status != OrganizationMembershipStatus.Active)
        {
            return;
        }

        WorkspaceOperationalAdmissionDecision admission =
            await operationalAdmission.EvaluateAsync(
                integrationEvent.ScopeId,
                cancellationToken).ConfigureAwait(false);
        if (admission.Outcome != WorkspaceOperationalAdmissionOutcome.Allowed)
        {
            throw new InvalidOperationException(
                "Workspace operational admission did not allow access-profile seeding.");
        }

        await provisioner.EnsureSeedProfilesAsync(
            integrationEvent.ScopeId,
            cancellationToken).ConfigureAwait(false);
    }
}
