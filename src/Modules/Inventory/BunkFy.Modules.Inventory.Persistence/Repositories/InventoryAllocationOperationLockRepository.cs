namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryAllocationOperationLockRepository(
    InventoryDbContext dbContext)
    : IInventoryAllocationOperationLock
{
    private const string CoordinateResourcePrefix =
        "bunkfy:inventory:allocation-coordinate:";

    public async Task<bool> TryAcquireExistingAsync(
        string tenantId,
        Guid allocationId,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateCoordinates(tenantId, allocationId);
        this.EnsureTransaction();

        if (dbContext.Database.IsRelational())
        {
            int affected = await this.AdvanceRelationalAsync(
                scopeId,
                allocationId,
                cancellationToken).ConfigureAwait(false);
            if (affected == 1)
            {
                return true;
            }

            return await this.HandleMissingExistingLockAsync(
                allocationId,
                cancellationToken).ConfigureAwait(false);
        }

        InventoryAllocationOperationLock? existing =
            await dbContext.AllocationOperationLocks
                .SingleOrDefaultAsync(
                    item => item.AllocationId == allocationId,
                    cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return await this.HandleMissingExistingLockAsync(
                allocationId,
                cancellationToken).ConfigureAwait(false);
        }

        existing.Touch();
        await dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public async Task AcquireCoordinateAsync(
        string tenantId,
        Guid allocationId,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateCoordinates(tenantId, allocationId);
        this.EnsureTransaction();

        if (dbContext.Database.IsRelational())
        {
            int affected = await this.AdvanceRelationalAsync(
                scopeId,
                allocationId,
                cancellationToken).ConfigureAwait(false);
            if (affected == 1)
            {
                return;
            }

            await EfTransactionKeyLock.AcquireAsync(
                dbContext,
                CoordinateResourcePrefix + scopeId + ':' +
                    allocationId.ToString("N"),
                cancellationToken).ConfigureAwait(false);
            affected = await this.AdvanceRelationalAsync(
                scopeId,
                allocationId,
                cancellationToken).ConfigureAwait(false);
            if (affected == 1)
            {
                return;
            }
        }
        else
        {
            InventoryAllocationOperationLock? existing =
                await dbContext.AllocationOperationLocks
                .SingleOrDefaultAsync(
                    item => item.AllocationId == allocationId,
                    cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                existing.Touch();
                await dbContext.SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        dbContext.AllocationOperationLocks.Add(new(
            allocationId,
            scopeId,
            allocationId));
        await dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<int> AdvanceRelationalAsync(
        string scopeId,
        Guid allocationId,
        CancellationToken cancellationToken) =>
        dbContext.AllocationOperationLocks
            .Where(resourceLock =>
                resourceLock.ScopeId == scopeId &&
                resourceLock.AllocationId == allocationId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    resourceLock => resourceLock.Revision,
                    resourceLock => resourceLock.Revision + 1),
                cancellationToken);

    private async Task<bool> HandleMissingExistingLockAsync(
        Guid allocationId,
        CancellationToken cancellationToken)
    {
        bool allocationExists = await dbContext.Allocations
            .AsNoTracking()
            .AnyAsync(
                allocation => allocation.Id == allocationId,
                cancellationToken).ConfigureAwait(false);
        if (!allocationExists)
        {
            return false;
        }

        throw new InvalidOperationException(
            "The inventory allocation operation lock is not provisioned.");
    }

    private string ValidateCoordinates(
        string tenantId,
        Guid allocationId)
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

        return scopeId;
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "An inventory allocation operation lock requires an active database transaction.");
        }
    }
}
