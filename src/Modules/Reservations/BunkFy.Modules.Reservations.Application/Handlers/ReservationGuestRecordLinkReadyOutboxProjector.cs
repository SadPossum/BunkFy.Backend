namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class ReservationGuestRecordLinkReadyOutboxProjector(
    IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationGuestRecordLinkReadyDomainEvent>
{
    public Task HandleAsync(
        ReservationGuestRecordLinkReadyDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationGuestRecordLinkReadyIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.OperationId,
                domainEvent.PropertyId,
                domainEvent.ReservationId,
                domainEvent.DispatchRevision),
            cancellationToken);
}
