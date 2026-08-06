namespace BunkFy.Modules.Ingestion.Persistence;

using BunkFy.Modules.Ingestion.Application.Ports;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionSourceGraphLock(IngestionDbContext dbContext)
    : IIngestionSourceOperationLock
{
    public async Task AcquireAsync(
        string tenantId,
        Guid sourceLinkId,
        CancellationToken cancellationToken)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            !string.Equals(
                scopeId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal) ||
            sourceLinkId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A scoped Ingestion source operation lock requires valid coordinates.");
        }

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "An Ingestion source operation lock requires an active database transaction.");
        }

        await EfTransactionKeyLock.AcquireAsync(
                dbContext,
                "bunkfy:ingestion:source:" + scopeId + ':' +
                sourceLinkId.ToString("N"),
                EfTransactionKeyLockMode.Exclusive,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
