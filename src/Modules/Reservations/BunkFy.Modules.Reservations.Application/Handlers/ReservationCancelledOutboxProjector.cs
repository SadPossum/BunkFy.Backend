namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Events;

internal sealed class ReservationCancelledOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationCancelledDomainEvent>
{
    public Task HandleAsync(ReservationCancelledDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationCancelledIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ReservationId,
                domainEvent.PropertyId,
                domainEvent.ReservationVersion,
                domainEvent.ActorId),
            cancellationToken);
}
