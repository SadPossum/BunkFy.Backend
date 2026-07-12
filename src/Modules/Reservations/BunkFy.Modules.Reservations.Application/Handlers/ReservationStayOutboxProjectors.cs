namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Events;

internal sealed class ReservationCheckedInOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationCheckedInDomainEvent>
{
    public Task HandleAsync(ReservationCheckedInDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationCheckedInIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ReservationId,
                domainEvent.PropertyId,
                domainEvent.BusinessDate,
                domainEvent.ActorId,
                domainEvent.ReservationVersion),
            cancellationToken);
}

internal sealed class ReservationNoShowRequestedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationNoShowRequestedDomainEvent>
{
    public Task HandleAsync(ReservationNoShowRequestedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        ReservationStayReleaseOutbox.EnqueueReleaseAsync(
            outboxWriters,
            domainEvent.EventId,
            domainEvent.ScopeId,
            domainEvent.OccurredAtUtc,
            domainEvent.ReservationId,
            domainEvent.AllocationId,
            domainEvent.ReleaseRequestId,
            domainEvent.ExpectedAllocationVersion,
            cancellationToken);
}

internal sealed class ReservationNoShowOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationNoShowDomainEvent>
{
    public Task HandleAsync(ReservationNoShowDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationNoShowIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ReservationId,
                domainEvent.PropertyId,
                domainEvent.BusinessDate,
                domainEvent.ActorId,
                domainEvent.ReservationVersion),
            cancellationToken);
}

internal sealed class ReservationCheckoutRequestedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationCheckoutRequestedDomainEvent>
{
    public Task HandleAsync(ReservationCheckoutRequestedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        ReservationStayReleaseOutbox.EnqueueReleaseAsync(
            outboxWriters,
            domainEvent.EventId,
            domainEvent.ScopeId,
            domainEvent.OccurredAtUtc,
            domainEvent.ReservationId,
            domainEvent.AllocationId,
            domainEvent.ReleaseRequestId,
            domainEvent.ExpectedAllocationVersion,
            cancellationToken);
}

internal sealed class ReservationCheckedOutOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<ReservationCheckedOutDomainEvent>
{
    public Task HandleAsync(ReservationCheckedOutDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationCheckedOutIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ReservationId,
                domainEvent.PropertyId,
                domainEvent.BusinessDate,
                domainEvent.ActorId,
                domainEvent.ReservationVersion),
            cancellationToken);
}

static file class ReservationStayReleaseOutbox
{
    public static Task EnqueueReleaseAsync(
        IOutboxWriterRegistry outboxWriters,
        Guid eventId,
        string scopeId,
        DateTimeOffset occurredAtUtc,
        Guid reservationId,
        Guid allocationId,
        Guid releaseRequestId,
        long expectedAllocationVersion,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new InventoryAllocationReleaseRequestedIntegrationEvent(
                eventId,
                scopeId,
                occurredAtUtc,
                reservationId,
                allocationId,
                releaseRequestId,
                expectedAllocationVersion),
            cancellationToken);
}
