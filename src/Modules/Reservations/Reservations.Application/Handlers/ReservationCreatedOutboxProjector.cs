namespace Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Inventory.Contracts;
using Reservations.Contracts;
using Reservations.Domain.Events;

internal sealed class ReservationCreatedOutboxProjector(IOutboxWriterRegistry outboxWriters, IIdGenerator idGenerator)
    : IDomainEventHandler<ReservationCreatedDomainEvent>
{
    public async Task HandleAsync(ReservationCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        IOutboxWriter outbox = outboxWriters.GetRequired(ReservationsModuleMetadata.Name);
        await outbox.EnqueueAsync(
            new ReservationCreatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ReservationId,
                domainEvent.PropertyId,
                domainEvent.Arrival,
                domainEvent.Departure,
                domainEvent.ReservationVersion),
            cancellationToken).ConfigureAwait(false);
        await outbox.EnqueueAsync(
            new InventoryAllocationRequestedIntegrationEvent(
                idGenerator.NewId(),
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ReservationId,
                domainEvent.AllocationRequestId,
                domainEvent.PropertyId,
                domainEvent.Arrival,
                domainEvent.Departure,
                domainEvent.InventoryUnitIds),
            cancellationToken).ConfigureAwait(false);
    }
}
