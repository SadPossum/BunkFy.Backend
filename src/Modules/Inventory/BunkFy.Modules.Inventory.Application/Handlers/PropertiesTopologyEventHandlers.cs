namespace BunkFy.Modules.Inventory.Application.Handlers;

using Gma.Framework.Messaging;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;

[IntegrationEventHandler(InventoryModuleMetadata.PropertyCreatedHandlerName)]
internal sealed class PropertyCreatedTopologyHandler(
    IInventoryTopologyRepository repository,
    InventoryUnitDefinitionPublisher definitions)
    : IIntegrationEventHandler<PropertyCreatedIntegrationEvent>
{
    public async Task HandleAsync(PropertyCreatedIntegrationEvent e, CancellationToken token)
    {
        await repository.ApplyPropertyAsync(
            new(e.ScopeId, e.PropertyId, e.Name, e.Code, e.TimeZoneId, e.Status, e.PropertyVersion), token).ConfigureAwait(false);
        await definitions.PublishPropertyAsync(e.PropertyId, e.OccurredAtUtc, token).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class PropertyUpdatedTopologyHandler(
    IInventoryTopologyRepository repository,
    InventoryUnitDefinitionPublisher definitions)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public async Task HandleAsync(PropertyUpdatedIntegrationEvent e, CancellationToken token)
    {
        await repository.ApplyPropertyAsync(
            new(e.ScopeId, e.PropertyId, e.Name, e.Code, e.TimeZoneId, e.Status, e.PropertyVersion), token).ConfigureAwait(false);
        await definitions.PublishPropertyAsync(e.PropertyId, e.OccurredAtUtc, token).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class PropertyRetiredTopologyHandler(
    IInventoryTopologyRepository repository,
    InventoryUnitDefinitionPublisher definitions)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public async Task HandleAsync(PropertyRetiredIntegrationEvent e, CancellationToken token)
    {
        await repository.ApplyPropertyAsync(
            new(e.ScopeId, e.PropertyId, null, null, null, PropertyStatus.Retired, e.PropertyVersion), token).ConfigureAwait(false);
        await definitions.PublishPropertyAsync(e.PropertyId, e.OccurredAtUtc, token).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.RoomCreatedHandlerName)]
internal sealed class RoomCreatedTopologyHandler(
    IInventoryTopologyRepository repository,
    IRoomInventoryConfigurationRepository configurations,
    InventoryUnitDefinitionPublisher definitions)
    : IIntegrationEventHandler<RoomCreatedIntegrationEvent>
{
    public async Task HandleAsync(RoomCreatedIntegrationEvent e, CancellationToken token)
    {
        await repository.ApplyRoomAsync(
            new(e.ScopeId, e.PropertyId, e.RoomId, e.Name, e.BuildingLabel, e.FloorLabel, e.Status, e.RoomVersion),
            token).ConfigureAwait(false);
        await configurations.EnsureAsync(e.ScopeId, e.PropertyId, e.RoomId, e.OccurredAtUtc, token).ConfigureAwait(false);
        await definitions.PublishRoomAsync(e.PropertyId, e.RoomId, e.OccurredAtUtc, token).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.RoomUpdatedHandlerName)]
internal sealed class RoomUpdatedTopologyHandler(
    IInventoryTopologyRepository repository,
    IRoomInventoryConfigurationRepository configurations,
    InventoryUnitDefinitionPublisher definitions)
    : IIntegrationEventHandler<RoomUpdatedIntegrationEvent>
{
    public async Task HandleAsync(RoomUpdatedIntegrationEvent e, CancellationToken token)
    {
        await repository.ApplyRoomAsync(
            new(e.ScopeId, e.PropertyId, e.RoomId, e.Name, e.BuildingLabel, e.FloorLabel, e.Status, e.RoomVersion),
            token).ConfigureAwait(false);
        await configurations.EnsureAsync(e.ScopeId, e.PropertyId, e.RoomId, e.OccurredAtUtc, token).ConfigureAwait(false);
        await definitions.PublishRoomAsync(e.PropertyId, e.RoomId, e.OccurredAtUtc, token).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.RoomRetiredHandlerName)]
internal sealed class RoomRetiredTopologyHandler(
    IInventoryTopologyRepository repository,
    IRoomInventoryConfigurationRepository configurations,
    InventoryUnitDefinitionPublisher definitions)
    : IIntegrationEventHandler<RoomRetiredIntegrationEvent>
{
    public async Task HandleAsync(RoomRetiredIntegrationEvent e, CancellationToken token)
    {
        await repository.ApplyRoomAsync(
            new(e.ScopeId, e.PropertyId, e.RoomId, null, null, null, RoomStatus.Retired, e.RoomVersion),
            token).ConfigureAwait(false);
        await configurations.EnsureAsync(e.ScopeId, e.PropertyId, e.RoomId, e.OccurredAtUtc, token).ConfigureAwait(false);
        await definitions.PublishRoomAsync(e.PropertyId, e.RoomId, e.OccurredAtUtc, token).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.BedAddedHandlerName)]
internal sealed class BedAddedTopologyHandler(
    IInventoryTopologyRepository repository,
    InventoryUnitDefinitionPublisher definitions)
    : IIntegrationEventHandler<BedAddedIntegrationEvent>
{
    public async Task HandleAsync(BedAddedIntegrationEvent e, CancellationToken token)
    {
        await repository.ApplyBedAsync(
            new(e.ScopeId, e.PropertyId, e.RoomId, e.BedId, e.Label, e.Status, e.BedVersion), token).ConfigureAwait(false);
        await definitions.PublishUnitAsync(e.PropertyId, e.RoomId, e.BedId, e.OccurredAtUtc, token).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.BedUpdatedHandlerName)]
internal sealed class BedUpdatedTopologyHandler(
    IInventoryTopologyRepository repository,
    InventoryUnitDefinitionPublisher definitions)
    : IIntegrationEventHandler<BedUpdatedIntegrationEvent>
{
    public async Task HandleAsync(BedUpdatedIntegrationEvent e, CancellationToken token)
    {
        await repository.ApplyBedAsync(
            new(e.ScopeId, e.PropertyId, e.RoomId, e.BedId, e.Label, e.Status, e.BedVersion), token).ConfigureAwait(false);
        await definitions.PublishUnitAsync(e.PropertyId, e.RoomId, e.BedId, e.OccurredAtUtc, token).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.BedRetiredHandlerName)]
internal sealed class BedRetiredTopologyHandler(
    IInventoryTopologyRepository repository,
    InventoryUnitDefinitionPublisher definitions)
    : IIntegrationEventHandler<BedRetiredIntegrationEvent>
{
    public async Task HandleAsync(BedRetiredIntegrationEvent e, CancellationToken token)
    {
        await repository.ApplyBedAsync(
            new(e.ScopeId, e.PropertyId, e.RoomId, e.BedId, null, BedStatus.Retired, e.BedVersion), token).ConfigureAwait(false);
        await definitions.PublishUnitAsync(e.PropertyId, e.RoomId, e.BedId, e.OccurredAtUtc, token).ConfigureAwait(false);
    }
}
