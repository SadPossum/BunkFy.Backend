namespace Reservations.Application.Ports;

using Gma.Framework.Pagination;
using Reservations.Contracts;
using Reservations.Domain.Aggregates;

public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);
    Task<Reservation?> GetAsync(Guid propertyId, Guid reservationId, CancellationToken cancellationToken);
    Task<Reservation?> GetAsyncByReservationId(Guid reservationId, CancellationToken cancellationToken);
    Task<bool> ExternalSourceExistsAsync(string sourceSystem, string sourceReference, CancellationToken cancellationToken);
    Task<ReservationListResponse> ListAsync(
        Guid propertyId,
        ReservationStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
