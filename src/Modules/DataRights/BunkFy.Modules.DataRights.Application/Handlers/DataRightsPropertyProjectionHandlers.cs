namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(DataRightsModuleMetadata.PropertyCreatedHandlerName)]
internal sealed class DataRightsPropertyCreatedHandler(IDataRightsPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyCreatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        properties.ApplyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.Name,
                integrationEvent.Status,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(DataRightsModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class DataRightsPropertyUpdatedHandler(IDataRightsPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyUpdatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        properties.ApplyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.Name,
                integrationEvent.Status,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(DataRightsModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class DataRightsPropertyRetiredHandler(IDataRightsPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(
        PropertyRetiredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        properties.ApplyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                string.Empty,
                PropertyStatus.Retired,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(DataRightsModuleMetadata.PropertyProcessingPolicyActivatedHandlerName)]
internal sealed class DataRightsPropertyProcessingPolicyActivatedHandler(
    IDataRightsPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyProcessingPolicyActivatedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyProcessingPolicyActivatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        properties.ApplyPolicyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                PropertyProcessingStatus.Enabled,
                integrationEvent.Binding,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(DataRightsModuleMetadata.PropertyProcessingSuspendedHandlerName)]
internal sealed class DataRightsPropertyProcessingSuspendedHandler(
    IDataRightsPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyProcessingSuspendedIntegrationEvent>
{
    public Task HandleAsync(
        PropertyProcessingSuspendedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        properties.ApplyPolicyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                PropertyProcessingStatus.Suspended,
                integrationEvent.Binding,
                integrationEvent.PropertyVersion),
            cancellationToken);
}
