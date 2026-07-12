namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Application.Ports;

internal sealed class ReservationExternalOperationRepository(ReservationsDbContext dbContext)
    : IReservationExternalOperationRepository
{
    public async Task<ReservationExternalOperationRecord?> GetAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        ReservationExternalOperation? operation = await dbContext.Set<ReservationExternalOperation>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == operationId, cancellationToken)
            .ConfigureAwait(false);
        return operation?.ToRecord();
    }

    public Task AddAsync(ReservationExternalOperationRecord operation, CancellationToken cancellationToken)
    {
        dbContext.Set<ReservationExternalOperation>().Add(new ReservationExternalOperation(operation));
        return Task.CompletedTask;
    }
}
