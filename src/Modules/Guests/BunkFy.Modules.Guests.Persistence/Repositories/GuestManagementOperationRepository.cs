namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestManagementOperationRepository(GuestsDbContext dbContext)
    : IGuestManagementOperationRepository
{
    public async Task<GuestManagementOperationRecord?> GetAsync(
        Guid guestId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        GuestManagementOperation? operation = await dbContext.ManagementOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.GuestId == guestId && item.Id == operationId,
                cancellationToken)
            .ConfigureAwait(false);
        return operation?.ToRecord();
    }

    public Task AddAsync(
        GuestManagementOperationRecord operation,
        CancellationToken cancellationToken)
    {
        dbContext.ManagementOperations.Add(new GuestManagementOperation(operation));
        return Task.CompletedTask;
    }

    public async Task DeleteForGuestAsync(
        Guid guestId,
        CancellationToken cancellationToken) =>
        _ = await dbContext.ManagementOperations
            .Where(operation => operation.GuestId == guestId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
