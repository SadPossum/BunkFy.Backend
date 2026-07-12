namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Events;

internal sealed class ReservationAllocationAmendmentRequestedOutboxProjector(
    IOutboxWriterRegistry outboxWriters,
    IIdGenerator idGenerator)
    : IDomainEventHandler<ReservationAllocationAmendmentRequestedDomainEvent>
{
    public Task HandleAsync(
        ReservationAllocationAmendmentRequestedDomainEvent domainEvent,
        CancellationToken cancellationToken) => outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
        new InventoryAllocationAmendmentRequestedIntegrationEvent(
            idGenerator.NewId(),
            domainEvent.ScopeId,
            domainEvent.OccurredAtUtc,
            domainEvent.AmendmentRequestId,
            domainEvent.AllocationId,
            domainEvent.ReservationId,
            domainEvent.PropertyId,
            domainEvent.ExpectedAllocationVersion,
            domainEvent.Arrival,
            domainEvent.Departure,
            domainEvent.InventoryUnitIds),
        cancellationToken);
}
