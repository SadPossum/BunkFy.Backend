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

    public Task<DataRightsExecutionWorkItem?> GetByCaseAsync(
        Guid propertyId,
        Guid caseId,
        CancellationToken cancellationToken) =>
        dbContext.ExecutionWorkItems.SingleOrDefaultAsync(
            workItem => workItem.PropertyId == propertyId && workItem.CaseId == caseId,
            cancellationToken);
}
