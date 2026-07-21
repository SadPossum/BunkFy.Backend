namespace BunkFy.Modules.Workspaces.Application.Handlers;

using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;

[IntegrationEventHandler(HandlerName, RequiresExplicitProducerBinding = true)]
internal sealed class OrganizationMembershipAccessProfileSeedHandler(
    WorkspaceAccessProvisioner provisioner)
    : IIntegrationEventHandler<OrganizationMembershipChangedIntegrationEvent>
{
    public const string HandlerName = "bunkfy-workspace-access-profile-seeds";

    public Task HandleAsync(
        OrganizationMembershipChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        integrationEvent.Status == OrganizationMembershipStatus.Active
            ? provisioner.EnsureSeedProfilesAsync(integrationEvent.ScopeId, cancellationToken)
            : Task.CompletedTask;
}
