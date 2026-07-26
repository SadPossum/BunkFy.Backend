namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionSourceOperationLockRepository(
    IngestionDbContext dbContext)
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

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "An Ingestion source operation lock requires an active database transaction.");
        }

        IngestionSourceOperationLock? resourceLock =
            await dbContext.Set<IngestionSourceOperationLock>()
                .SingleOrDefaultAsync(
                    item => item.SourceLinkId == sourceLinkId,
                    cancellationToken)
                .ConfigureAwait(false);
        if (resourceLock is null)
        {
            dbContext.Set<IngestionSourceOperationLock>().Add(new(
                Guid.NewGuid(),
                scopeId,
                sourceLinkId));
        }
        else
        {
            resourceLock.Touch();
        }

        await dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
