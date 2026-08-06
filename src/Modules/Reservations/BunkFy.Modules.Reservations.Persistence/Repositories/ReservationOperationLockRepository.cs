namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationOperationLockRepository(
    ReservationsDbContext dbContext)
    : IReservationOperationLock
{
    public async Task<bool> TryAcquireExistingAsync(
        string tenantId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateCoordinates(tenantId, reservationId);
        this.EnsureTransaction();

        if (dbContext.Database.IsRelational())
        {
            int affected = await dbContext.OperationLocks
                .Where(resourceLock =>
                    resourceLock.ScopeId == scopeId &&
                    resourceLock.ReservationId == reservationId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        resourceLock => resourceLock.Revision,
                        resourceLock => resourceLock.Revision + 1),
                    cancellationToken)
                .ConfigureAwait(false);
            if (affected == 1)
            {
                return true;
            }

            return await this.HandleMissingExistingLockAsync(
                reservationId,
                cancellationToken).ConfigureAwait(false);
        }

        ReservationOperationLock? existing = await dbContext.OperationLocks
            .SingleOrDefaultAsync(
                resourceLock => resourceLock.ReservationId == reservationId,
                cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return await this.HandleMissingExistingLockAsync(
                reservationId,
                cancellationToken).ConfigureAwait(false);
        }

        existing.Touch();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task AcquireCoordinateAsync(
        string tenantId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateCoordinates(tenantId, reservationId);
        this.EnsureTransaction();

        ReservationOperationLock? resourceLock = await dbContext.OperationLocks
            .SingleOrDefaultAsync(
                item => item.ReservationId == reservationId,
                cancellationToken).ConfigureAwait(false);
        if (resourceLock is null)
        {
            dbContext.OperationLocks.Add(new(
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

    private async Task<bool> HandleMissingExistingLockAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        bool reservationExists = await dbContext.Reservations
            .AsNoTracking()
            .AnyAsync(
                reservation => reservation.Id == reservationId,
                cancellationToken).ConfigureAwait(false);
        if (!reservationExists)
        {
            return false;
        }

        throw new InvalidOperationException(
            "The reservation operation lock is not provisioned.");
    }

    private string ValidateCoordinates(
        string tenantId,
        Guid reservationId)
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

        return scopeId;
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A reservation operation lock requires an active database transaction.");
        }
    }
}
