namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationOperationLockRepository(
    ReservationsDbContext dbContext)
    : IReservationOperationLock
{
    public async Task AcquireAsync(
        string tenantId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            !string.Equals(
                scopeId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal) ||
            reservationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A scoped reservation operation lock requires valid coordinates.");
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A reservation operation lock requires an active database transaction.");
        }

        ReservationOperationLock? resourceLock =
            await dbContext.Set<ReservationOperationLock>()
                .SingleOrDefaultAsync(
                    item => item.ReservationId == reservationId,
                    cancellationToken).ConfigureAwait(false);
        if (resourceLock is null)
        {
            dbContext.Set<ReservationOperationLock>().Add(new(
                Guid.NewGuid(),
                scopeId,
                reservationId));
        }
        else
        {
            resourceLock.Touch();
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
