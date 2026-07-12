namespace BunkFy.Modules.Reservations.Application.Ports;

using Gma.Framework.Pagination;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);
    Task<Reservation?> GetAsync(Guid propertyId, Guid reservationId, CancellationToken cancellationToken);
    Task<Reservation?> GetAsyncByReservationId(Guid reservationId, CancellationToken cancellationToken);
    Task<Reservation?> GetByExternalSourceAsync(string sourceSystem, string sourceReference, CancellationToken cancellationToken);
    Task<bool> ExternalSourceExistsAsync(string sourceSystem, string sourceReference, CancellationToken cancellationToken);
    Task<ReservationListResponse> ListAsync(
        Guid propertyId,
        ReservationStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
