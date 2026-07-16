namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Events;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;

internal sealed class BedRetirementFinalizationRequestedOutboxProjector(
    IOutboxWriterRegistry outboxWriters,
    IIdGenerator idGenerator)
    : IDomainEventHandler<BedRetirementFinalizationRequestedDomainEvent>
{
    public Task HandleAsync(
        BedRetirementFinalizationRequestedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new BedRetirementFinalizationRequestedIntegrationEvent(
                idGenerator.NewId(),
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.TopologyChangeId,
                domainEvent.PropertyId,
                domainEvent.RoomId,
                domainEvent.BedId),
            cancellationToken);
}
