namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using Gma.Framework.Naming;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryRoomManagementLock(
    InventoryDbContext dbContext)
    : IInventoryRoomManagementLock
{
    private const string ResourcePrefix =
        "bunkfy:inventory:management-room:";

    public async Task AcquireAsync(
        string tenantId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        if (!TenantIds.TryNormalize(tenantId, out string? canonicalTenantId) ||
            !string.Equals(
                canonicalTenantId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal) ||
            roomId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A scoped Inventory Room management lock requires valid coordinates.");
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "An Inventory Room management lock requires an active database transaction.");
        }

        await dbContext.AcquireOperationalMutationAdmissionAsync(
                cancellationToken)
            .ConfigureAwait(false);
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        await EfTransactionKeyLock.AcquireAsync(
                dbContext,
                ResourcePrefix + canonicalTenantId + ':' +
                roomId.ToString("N"),
                EfTransactionKeyLockMode.Exclusive,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
