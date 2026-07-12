namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class ReservationInboxDomainEventDispatcher(IDomainEventDispatcher dispatcher)
{
    public Task DispatchAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        return reservation.DomainEvents.Count == 0
            ? Task.CompletedTask
            : dispatcher.DispatchAsync(reservation.DomainEvents.ToArray(), cancellationToken);
    }
}
