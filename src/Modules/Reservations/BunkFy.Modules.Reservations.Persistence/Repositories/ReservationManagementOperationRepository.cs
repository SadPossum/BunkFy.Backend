namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationManagementOperationRepository(
    ReservationsDbContext dbContext)
    : IReservationManagementOperationRepository
{
    public async Task<ReservationManagementOperationRecord?> GetAsync(
        Guid reservationId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        ReservationManagementOperation? operation = await dbContext
            .Set<ReservationManagementOperation>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ReservationId == reservationId &&
                    item.Id == operationId,
                cancellationToken)
            .ConfigureAwait(false);
        return operation?.ToRecord();
    }

    public Task AddAsync(
        ReservationManagementOperationRecord operation,
        CancellationToken cancellationToken)
    {
        dbContext.Set<ReservationManagementOperation>()
            .Add(new ReservationManagementOperation(operation));
        return Task.CompletedTask;
    }
}
