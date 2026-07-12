namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.Events;

internal sealed class ReservationDetailsHistoryProjector(IReservationDetailsHistoryWriter history)
    : IDomainEventHandler<ReservationDetailsChangedDomainEvent>
{
    public Task HandleAsync(
        ReservationDetailsChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        history.AppendAsync(domainEvent, cancellationToken);
}
