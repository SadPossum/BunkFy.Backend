namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Events;

internal sealed class ReservationGuestStayChangedOutboxProjector(
    IOutboxWriterRegistry outboxWriters,
    IIdGenerator ids)
    : IDomainEventHandler<ReservationGuestStayChangedDomainEvent>
{
    public Task HandleAsync(
        ReservationGuestStayChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationGuestStayChangedIntegrationEvent(
                ids.NewId(),
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.ReservationId,
                domainEvent.GuestId,
                (GuestStayRole)(int)domainEvent.Role,
                domainEvent.Arrival,
                domainEvent.Departure,
                MapStatus(domainEvent.Status),
                domainEvent.CheckedInBusinessDate,
                domainEvent.NoShowBusinessDate,
                domainEvent.CheckedOutBusinessDate,
                domainEvent.IsCurrentParticipant,
                domainEvent.ReservationVersion),
            cancellationToken);

    private static GuestStayStatus MapStatus(ReservationState status) => status switch
    {
        ReservationState.PendingAllocation => GuestStayStatus.PendingAllocation,
        ReservationState.Confirmed => GuestStayStatus.Confirmed,
        ReservationState.AllocationRejected => GuestStayStatus.AllocationRejected,
        ReservationState.CancellationPending => GuestStayStatus.CancellationPending,
        ReservationState.Cancelled => GuestStayStatus.Cancelled,
        ReservationState.CheckedIn => GuestStayStatus.CheckedIn,
        ReservationState.NoShowPending => GuestStayStatus.NoShowPending,
        ReservationState.NoShow => GuestStayStatus.NoShow,
        ReservationState.CheckoutPending => GuestStayStatus.CheckoutPending,
        ReservationState.CheckedOut => GuestStayStatus.CheckedOut,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
