namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Messaging;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Properties.Contracts;

[IntegrationEventHandler(IngestionModuleMetadata.PropertyCreatedHandlerName)]
internal sealed class IngestionPropertyCreatedHandler(IIngestionPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyCreatedIntegrationEvent>
{
    public Task HandleAsync(PropertyCreatedIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyAsync(
            new(e.ScopeId, e.PropertyId, e.Name, e.Code, e.Status == PropertyStatus.Active, e.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(IngestionModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class IngestionPropertyUpdatedHandler(IIngestionPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public Task HandleAsync(PropertyUpdatedIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyAsync(
            new(e.ScopeId, e.PropertyId, e.Name, e.Code, e.Status == PropertyStatus.Active, e.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(IngestionModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class IngestionPropertyRetiredHandler(IIngestionPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(PropertyRetiredIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyAsync(
            new(e.ScopeId, e.PropertyId, null, null, IsActive: false, e.PropertyVersion),
            cancellationToken);
}
