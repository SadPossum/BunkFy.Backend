namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsExecutionWorkItemRepository(DataRightsDbContext dbContext)
    : IDataRightsExecutionWorkItemRepository
{
    public Task AddAsync(
        DataRightsExecutionWorkItem workItem,
        CancellationToken cancellationToken)
    {
        dbContext.ExecutionWorkItems.Add(workItem);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<DataRightsExecutionWorkItem>> ListByBatchAsync(
        Guid propertyId,
        Guid caseId,
        Guid batchId,
        CancellationToken cancellationToken) =>
        await dbContext.ExecutionWorkItems
            .Where(workItem =>
                workItem.PropertyId == propertyId &&
                workItem.CaseId == caseId &&
                workItem.BatchId == batchId)
            .OrderBy(workItem => workItem.OwnerKey)
            .ThenBy(workItem => workItem.RecordType)
            .ThenBy(workItem => workItem.RecordId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<DataRightsExecutionWorkItem?> GetAsync(
        Guid propertyId,
        Guid caseId,
        Guid workItemId,
        CancellationToken cancellationToken) =>
        dbContext.ExecutionWorkItems.SingleOrDefaultAsync(
            workItem =>
                workItem.PropertyId == propertyId &&
                workItem.CaseId == caseId &&
                workItem.Id == workItemId,
            cancellationToken);
}
