namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;

[IntegrationEventHandler(HandlerName, RequiresExplicitProducerBinding = true)]
internal sealed class OrganizationMembershipAccessProfileSeedHandler(
    WorkspaceAccessProvisioner provisioner)
    : IIntegrationEventHandler<OrganizationMembershipChangedIntegrationEvent>
{
    public const string HandlerName = WorkspacesModuleMetadata.MembershipAccessSeedHandlerName;

    public Task HandleAsync(
        OrganizationMembershipChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        integrationEvent.Status == OrganizationMembershipStatus.Active
            ? provisioner.EnsureSeedProfilesAsync(integrationEvent.ScopeId, cancellationToken)
            : Task.CompletedTask;
}
