namespace Inventory.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Inventory.Contracts;
using Inventory.Domain.Events;

internal sealed class ManualInventoryBlockReleasedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ManualInventoryBlockReleasedDomainEvent>
{
    public Task HandleAsync(ManualInventoryBlockReleasedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new ManualInventoryBlockReleasedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.BlockId,
                domainEvent.PropertyId,
                domainEvent.InventoryUnitId,
                domainEvent.BlockVersion),
            cancellationToken);
}
