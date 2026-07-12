namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Contracts;

public interface IReservationDetailsHistoryReader
{
    Task<IReadOnlyList<ReservationDetailsHistoryItem>> ListAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken);
}
