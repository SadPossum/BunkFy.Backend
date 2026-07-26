namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsExecutionBatchRepository(DataRightsDbContext dbContext)
    : IDataRightsExecutionBatchRepository
{
    public Task AddAsync(
        DataRightsExecutionBatch batch,
        CancellationToken cancellationToken)
    {
        dbContext.ExecutionBatches.Add(batch);
        return Task.CompletedTask;
    }

    public Task<DataRightsExecutionBatch?> GetByCaseAsync(
        Guid propertyId,
        Guid caseId,
        CancellationToken cancellationToken) =>
        dbContext.ExecutionBatches.SingleOrDefaultAsync(
            batch => batch.PropertyId == propertyId && batch.CaseId == caseId,
            cancellationToken);
}
