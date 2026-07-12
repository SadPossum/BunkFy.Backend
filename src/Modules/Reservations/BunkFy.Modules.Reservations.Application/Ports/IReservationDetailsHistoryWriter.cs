namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Domain.Events;

public interface IReservationDetailsHistoryWriter
{
    Task AppendAsync(ReservationDetailsChangedDomainEvent change, CancellationToken cancellationToken);
}
