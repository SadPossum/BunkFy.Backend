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
        properties.ApplyAsync(
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
        properties.ApplyAsync(
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
        properties.ApplyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                string.Empty,
                PropertyStatus.Retired,
                integrationEvent.PropertyVersion),
            cancellationToken);
}
