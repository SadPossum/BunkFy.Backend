namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(WorkspacesModuleMetadata.PropertyCreatedHandlerName)]
internal sealed class WorkspacePropertyCreatedHandler(IWorkspacePropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyCreatedIntegrationEvent>
{
    public Task HandleAsync(PropertyCreatedIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyAsync(
            new(e.ScopeId, e.PropertyId, e.Name, e.Status, e.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(WorkspacesModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class WorkspacePropertyUpdatedHandler(IWorkspacePropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public Task HandleAsync(PropertyUpdatedIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyAsync(
            new(e.ScopeId, e.PropertyId, e.Name, e.Status, e.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(WorkspacesModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class WorkspacePropertyRetiredHandler(IWorkspacePropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(PropertyRetiredIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyAsync(
            new(e.ScopeId, e.PropertyId, string.Empty, PropertyStatus.Retired, e.PropertyVersion),
            cancellationToken);
}
