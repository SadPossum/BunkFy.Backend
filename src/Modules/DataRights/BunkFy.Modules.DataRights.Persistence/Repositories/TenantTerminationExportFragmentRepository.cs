namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class TenantTerminationExportFragmentRepository(
    DataRightsDbContext dbContext)
    : ITenantTerminationExportFragmentRepository
{
    public Task AddAsync(
        TenantTerminationExportFragment fragment,
        CancellationToken cancellationToken)
    {
        dbContext.TenantTerminationExportFragments.Add(fragment);
        return Task.CompletedTask;
    }

    public Task<TenantTerminationExportFragment?> GetAsync(
        Guid workItemId,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationExportFragments.SingleOrDefaultAsync(
            fragment => fragment.Id == workItemId,
            cancellationToken);

    public Task<TenantTerminationExportFragment?> GetByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationExportFragments.SingleOrDefaultAsync(
            fragment => fragment.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task<IReadOnlyList<TenantTerminationExportFragment>>
        ListAsync(
            Guid processId,
            long exportOperationRevision,
            CancellationToken cancellationToken) =>
        await dbContext.TenantTerminationExportFragments
            .Where(fragment =>
                fragment.ProcessId == processId &&
                fragment.ExportOperationRevision == exportOperationRevision)
            .OrderBy(fragment => fragment.OwnerKey)
            .ThenBy(fragment => fragment.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
