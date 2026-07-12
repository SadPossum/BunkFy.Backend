namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Reservations;

public interface IReservationDispatchRepository
{
    Task<ReservationDispatch?> GetAsync(Guid operationId, CancellationToken cancellationToken);
    Task<ReservationDispatch?> FindByTriggerAsync(
        ReservationDispatchTriggerKind triggerKind,
        Guid triggerId,
        CancellationToken cancellationToken);
    Task<ReservationDispatch?> FindAcceptedCancellationAsync(Guid reservationId, CancellationToken cancellationToken);
    Task AddAsync(ReservationDispatch dispatch, CancellationToken cancellationToken);
}
