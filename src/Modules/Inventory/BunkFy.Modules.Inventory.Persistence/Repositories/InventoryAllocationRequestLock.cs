namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryAllocationRequestLock(
    InventoryDbContext dbContext)
    : IInventoryAllocationRequestLock
{
    private const string RequestResourcePrefix =
        "bunkfy:inventory:allocation-request:";
    private const string ReservationResourcePrefix =
        "bunkfy:inventory:allocation-reservation:";

    public async Task AcquireAsync(
        string tenantId,
        Guid allocationRequestId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            !string.Equals(
                scopeId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal) ||
            allocationRequestId == Guid.Empty ||
            reservationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A scoped inventory allocation request lock requires valid coordinates.");
        }

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "An inventory allocation request lock requires an active database transaction.");
        }

        await EfTransactionKeyLock.AcquireAsync(
            dbContext,
            RequestResourcePrefix + scopeId + ':' +
                allocationRequestId.ToString("N"),
            cancellationToken).ConfigureAwait(false);
        await EfTransactionKeyLock.AcquireAsync(
            dbContext,
            ReservationResourcePrefix + scopeId + ':' +
                reservationId.ToString("N"),
            cancellationToken).ConfigureAwait(false);
    }
}
