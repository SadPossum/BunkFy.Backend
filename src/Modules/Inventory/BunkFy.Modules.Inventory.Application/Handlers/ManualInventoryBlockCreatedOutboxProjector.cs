namespace BunkFy.Modules.Inventory.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Events;

internal sealed class ManualInventoryBlockCreatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ManualInventoryBlockCreatedDomainEvent>
{
    public Task HandleAsync(ManualInventoryBlockCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new ManualInventoryBlockCreatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.BlockId,
                domainEvent.BlockGroupId,
                domainEvent.PropertyId,
                domainEvent.InventoryUnitId,
                domainEvent.Arrival,
                domainEvent.Departure,
                domainEvent.Reason,
                domainEvent.BlockVersion),
            cancellationToken);
}
