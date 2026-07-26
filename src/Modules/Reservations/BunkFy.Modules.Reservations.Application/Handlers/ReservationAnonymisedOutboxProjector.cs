namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class ReservationAnonymisedOutboxProjector(
    IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationAnonymisedDomainEvent>
{
    public Task HandleAsync(
        ReservationAnonymisedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        IOutboxWriter outbox = outboxWriters.GetRequired(
            ReservationsModuleMetadata.Name);
        return outbox.EnqueueAsync(
            new ReservationAnonymisedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.ReservationId,
                domainEvent.ReservationVersion,
                domainEvent.DetailsRevision),
            cancellationToken);
    }
}
