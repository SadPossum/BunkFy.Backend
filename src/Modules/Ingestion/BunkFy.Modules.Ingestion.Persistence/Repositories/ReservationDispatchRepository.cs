namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationDispatchRepository(IngestionDbContext dbContext) : IReservationDispatchRepository
{
    public Task<ReservationDispatch?> GetAsync(Guid operationId, CancellationToken cancellationToken) =>
        dbContext.ReservationDispatches.FirstOrDefaultAsync(dispatch => dispatch.Id == operationId, cancellationToken);

    public Task<ReservationDispatch?> FindByTriggerAsync(
        ReservationDispatchTriggerKind triggerKind,
        Guid triggerId,
        CancellationToken cancellationToken) =>
        dbContext.ReservationDispatches.FirstOrDefaultAsync(
            dispatch => dispatch.TriggerKind == triggerKind && dispatch.TriggerId == triggerId,
            cancellationToken);

    public Task<ReservationDispatch?> FindAcceptedCancellationAsync(
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.ReservationDispatches.FirstOrDefaultAsync(
            dispatch => dispatch.ReservationId == reservationId &&
                        dispatch.Kind == ReservationDispatchKind.Cancel &&
                        dispatch.State == ReservationDispatchState.Accepted,
            cancellationToken);

    public Task AddAsync(ReservationDispatch dispatch, CancellationToken cancellationToken)
    {
        dbContext.ReservationDispatches.Add(dispatch);
        return Task.CompletedTask;
    }
}
