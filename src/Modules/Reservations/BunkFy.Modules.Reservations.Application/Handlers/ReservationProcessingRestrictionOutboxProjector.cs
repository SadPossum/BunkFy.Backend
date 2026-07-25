namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class ReservationProcessingRestrictionOutboxProjector(
    IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationProcessingRestrictionChangedDomainEvent>
{
    public Task HandleAsync(
        ReservationProcessingRestrictionChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationProcessingRestrictionChangedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.ReservationId,
                domainEvent.ContractVersion,
                domainEvent.ProjectionRevision,
                domainEvent.IsRestricted),
            cancellationToken);
}
