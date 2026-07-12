namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Reservations;

public interface IReservationSourceLinkRepository
{
    Task<ReservationSourceLink?> GetAsync(Guid sourceLinkId, CancellationToken cancellationToken);
    Task<ReservationSourceLink?> FindBySourceAsync(Guid connectionId, string sourceReference, CancellationToken cancellationToken);
    Task<ReservationSourceLink?> FindByReservationAsync(Guid reservationId, CancellationToken cancellationToken);
    Task AddAsync(ReservationSourceLink sourceLink, CancellationToken cancellationToken);
}
