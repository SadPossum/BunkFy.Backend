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
        properties.ApplySnapshotAsync(
            new(
                e.ScopeId,
                e.PropertyId,
                e.Name,
                e.Code,
                e.Status == PropertyStatus.Active,
                PropertyProcessingStatus.Unconfigured,
                GovernancePolicy: null,
                e.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(IngestionModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class IngestionPropertyUpdatedHandler(IIngestionPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public Task HandleAsync(PropertyUpdatedIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyTopologyAsync(
            new(e.ScopeId, e.PropertyId, e.Name, e.Code, e.Status == PropertyStatus.Active, e.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(IngestionModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class IngestionPropertyRetiredHandler(IIngestionPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(PropertyRetiredIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyTopologyAsync(
            new(e.ScopeId, e.PropertyId, null, null, IsActive: false, e.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(IngestionModuleMetadata.PropertyProcessingPolicyActivatedHandlerName)]
internal sealed class IngestionPropertyProcessingPolicyActivatedHandler(
    IIngestionPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyProcessingPolicyActivatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyProcessingPolicyActivatedIntegrationEvent e,
        CancellationToken cancellationToken) =>
        properties.ApplyPolicyAsync(
            new(
                e.ScopeId,
                e.PropertyId,
                PropertyProcessingStatus.Enabled,
                e.Binding,
                e.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(IngestionModuleMetadata.PropertyProcessingSuspendedHandlerName)]
internal sealed class IngestionPropertyProcessingSuspendedHandler(
    IIngestionPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyProcessingSuspendedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyProcessingSuspendedIntegrationEvent e,
        CancellationToken cancellationToken) =>
        properties.ApplyPolicyAsync(
            new(
                e.ScopeId,
                e.PropertyId,
                PropertyProcessingStatus.Suspended,
                e.Binding,
                e.PropertyVersion),
            cancellationToken);
}
