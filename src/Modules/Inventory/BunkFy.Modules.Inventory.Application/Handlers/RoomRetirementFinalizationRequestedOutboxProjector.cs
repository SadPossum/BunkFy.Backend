namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Events;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;

internal sealed class RoomRetirementFinalizationRequestedOutboxProjector(
    IOutboxWriterRegistry outboxWriters,
    IIdGenerator idGenerator)
    : IDomainEventHandler<RoomRetirementFinalizationRequestedDomainEvent>
{
    public Task HandleAsync(
        RoomRetirementFinalizationRequestedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new RoomRetirementFinalizationRequestedIntegrationEvent(
                idGenerator.NewId(),
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.TopologyChangeId,
                domainEvent.PropertyId,
                domainEvent.RoomId),
            cancellationToken);
}
