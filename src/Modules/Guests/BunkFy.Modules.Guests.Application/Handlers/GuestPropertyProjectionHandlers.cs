namespace BunkFy.Modules.Guests.Application.Handlers;

using Gma.Framework.Messaging;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Properties.Contracts;

[IntegrationEventHandler(GuestsModuleMetadata.PropertyCreatedHandlerName)]
internal sealed class GuestPropertyCreatedHandler(IGuestPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyCreatedIntegrationEvent>
{
    public Task HandleAsync(PropertyCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        properties.ApplyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.Name,
                integrationEvent.Status,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(GuestsModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class GuestPropertyUpdatedHandler(IGuestPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public Task HandleAsync(PropertyUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        properties.ApplyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.Name,
                integrationEvent.Status,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(GuestsModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class GuestPropertyRetiredHandler(IGuestPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(PropertyRetiredIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        properties.ApplyTopologyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                string.Empty,
                PropertyStatus.Retired,
                integrationEvent.PropertyVersion),
            cancellationToken);
}

[IntegrationEventHandler(GuestsModuleMetadata.PropertyProcessingPolicyActivatedHandlerName)]
internal sealed class GuestPropertyProcessingPolicyActivatedHandler(IGuestPropertyProjectionRepository properties)
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

[IntegrationEventHandler(GuestsModuleMetadata.PropertyProcessingSuspendedHandlerName)]
internal sealed class GuestPropertyProcessingSuspendedHandler(IGuestPropertyProjectionRepository properties)
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
