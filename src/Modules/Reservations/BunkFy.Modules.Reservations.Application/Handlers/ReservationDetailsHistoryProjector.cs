namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.Events;

internal sealed class ReservationDetailsHistoryProjector(
    IReservationDetailsHistoryWriter history,
    IReservationArrivalReminderRepository reminders)
    : IDomainEventHandler<ReservationDetailsChangedDomainEvent>
{
    public async Task HandleAsync(
        ReservationDetailsChangedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await history.AppendAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        await reminders.RefreshReservationAsync(
            new(
                domainEvent.ScopeId,
                domainEvent.ReservationId,
                domainEvent.PropertyId,
                domainEvent.After.Arrival,
                domainEvent.After.ExpectedArrivalTime,
                domainEvent.ToRevision),
            cancellationToken).ConfigureAwait(false);
    }
}
