namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsProcessingLedgerRepository(DataRightsDbContext dbContext)
    : IDataRightsProcessingLedgerRepository
{
    public Task AddAsync(
        DataRightsProcessingLedgerEntry entry,
        CancellationToken cancellationToken)
    {
        dbContext.ProcessingLedgerEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<DataRightsProcessingLedgerEntry?> GetByWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken) =>
        dbContext.ProcessingLedgerEntries.SingleOrDefaultAsync(
            entry => entry.WorkItemId == workItemId,
            cancellationToken);

    public Task<DataRightsProcessingLedgerEntry?> GetByOwnerReceiptAsync(
        string ownerKey,
        Guid ownerReceiptId,
        CancellationToken cancellationToken)
    {
        string owner = ownerKey?.Trim() ?? string.Empty;
        return dbContext.ProcessingLedgerEntries.SingleOrDefaultAsync(
            entry =>
                entry.OwnerKey == owner &&
                entry.OwnerReceiptId == ownerReceiptId,
            cancellationToken);
    }

    public Task<DataRightsProcessingLedgerEntry?> GetLatestAsync(
        CancellationToken cancellationToken) =>
        dbContext.ProcessingLedgerEntries
            .OrderByDescending(entry => entry.TenantSequence)
            .FirstOrDefaultAsync(cancellationToken);
}
