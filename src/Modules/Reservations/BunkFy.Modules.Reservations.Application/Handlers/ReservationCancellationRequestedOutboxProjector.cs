namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Events;

internal sealed class ReservationCancellationRequestedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationCancellationRequestedDomainEvent>
{
    public Task HandleAsync(ReservationCancellationRequestedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new InventoryAllocationReleaseRequestedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ReservationId,
                domainEvent.AllocationId,
                domainEvent.ReleaseRequestId,
                domainEvent.ExpectedAllocationVersion),
            cancellationToken);
}
