namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryAllocationOperationLockRepository(
    InventoryDbContext dbContext)
    : IInventoryAllocationOperationLock
{
    public async Task AcquireAsync(
        string tenantId,
        Guid allocationId,
        CancellationToken cancellationToken)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            !string.Equals(
                scopeId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal) ||
            allocationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A scoped inventory allocation operation lock requires valid coordinates.");
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "An inventory allocation operation lock requires an active database transaction.");
        }

        InventoryAllocationOperationLock? resourceLock =
            await dbContext.AllocationOperationLocks
                .SingleOrDefaultAsync(
                    item => item.AllocationId == allocationId,
                    cancellationToken).ConfigureAwait(false);
        if (resourceLock is null)
        {
            dbContext.AllocationOperationLocks.Add(
                new(
                    allocationId,
                    scopeId,
                    allocationId));
        }
        else
        {
            resourceLock.Touch();
        }

        await dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
