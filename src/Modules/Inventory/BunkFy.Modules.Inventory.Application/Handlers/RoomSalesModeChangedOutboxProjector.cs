namespace BunkFy.Modules.Inventory.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.Events;

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
                domainEvent.ConfigurationVersion,
                domainEvent.ActorId),
            cancellationToken).ConfigureAwait(false);
        await definitions.PublishRoomAsync(
            domainEvent.PropertyId,
            domainEvent.RoomId,
            domainEvent.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);
    }
}
