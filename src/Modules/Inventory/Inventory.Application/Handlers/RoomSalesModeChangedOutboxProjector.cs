namespace Inventory.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Inventory.Contracts;
using Inventory.Domain.Aggregates;
using Inventory.Domain.Events;

internal sealed class RoomSalesModeChangedOutboxProjector(
    IOutboxWriterRegistry outboxWriters,
    InventoryUnitDefinitionPublisher definitions)
    : IDomainEventHandler<RoomSalesModeChangedDomainEvent>
{
    public async Task HandleAsync(RoomSalesModeChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new RoomSalesModeChangedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.RoomId,
                domainEvent.SalesMode == RoomSalesMode.RoomLevel
                    ? InventorySalesMode.RoomLevel
                    : InventorySalesMode.BedLevel,
                domainEvent.ConfigurationVersion),
            cancellationToken).ConfigureAwait(false);
        await definitions.PublishRoomAsync(
            domainEvent.PropertyId,
            domainEvent.RoomId,
            domainEvent.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);
    }
}
