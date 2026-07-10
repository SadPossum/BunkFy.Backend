namespace Inventory.Application.Handlers;

using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Inventory.Application.Ports;
using Inventory.Contracts;

internal sealed class InventoryUnitDefinitionPublisher(
    IInventoryTopologyRepository topology,
    IOutboxWriterRegistry outboxWriters,
    IIdGenerator idGenerator)
{
    public Task PublishPropertyAsync(
        Guid propertyId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        this.PublishAsync(propertyId, null, null, occurredAtUtc, cancellationToken);

    public Task PublishRoomAsync(
        Guid propertyId,
        Guid roomId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        this.PublishAsync(propertyId, roomId, null, occurredAtUtc, cancellationToken);

    public Task PublishUnitAsync(
        Guid propertyId,
        Guid roomId,
        Guid inventoryUnitId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        this.PublishAsync(propertyId, roomId, inventoryUnitId, occurredAtUtc, cancellationToken);

    private async Task PublishAsync(
        Guid propertyId,
        Guid? roomId,
        Guid? inventoryUnitId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<InventoryUnitDefinitionSnapshot> definitions = await topology
            .GetUnitDefinitionsAsync(propertyId, roomId, inventoryUnitId, touchVersions: true, cancellationToken)
            .ConfigureAwait(false);
        IOutboxWriter writer = outboxWriters.GetRequired(InventoryModuleMetadata.Name);
        foreach (InventoryUnitDefinitionSnapshot definition in definitions)
        {
            await writer.EnqueueAsync(
                new InventoryUnitDefinitionChangedIntegrationEvent(
                    idGenerator.NewId(),
                    definition.ScopeId,
                    occurredAtUtc,
                    definition.InventoryUnitId,
                    definition.PropertyId,
                    definition.RoomId,
                    definition.BedId,
                    definition.Kind,
                    definition.Label,
                    definition.IsTopologyActive,
                    definition.IsSellable,
                    definition.ConfigurationVersion,
                    definition.UnitVersion),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
