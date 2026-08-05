namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Pagination;

public interface IReservationDetailsHistoryReader
{
    Task<ReservationDetailsHistoryListResponse> ListAsync(
        Guid propertyId,
        Guid reservationId,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
