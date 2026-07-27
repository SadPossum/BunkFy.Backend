namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsCorrectionExecutionRepository(
    DataRightsDbContext dbContext)
    : IDataRightsCorrectionExecutionRepository
{
    public Task AddAsync(
        DataRightsCorrectionExecution execution,
        CancellationToken cancellationToken)
    {
        dbContext.CorrectionExecutions.Add(execution);
        return Task.CompletedTask;
    }

    public Task<DataRightsCorrectionExecution?> GetAsync(
        Guid propertyId,
        Guid caseId,
        Guid executionId,
        CancellationToken cancellationToken) =>
        dbContext.CorrectionExecutions.FirstOrDefaultAsync(
            execution =>
                execution.PropertyId == propertyId &&
                execution.CaseId == caseId &&
                execution.Id == executionId,
            cancellationToken);

    public Task<DataRightsCorrectionExecution?> GetByCaseAsync(
        Guid propertyId,
        Guid caseId,
        CancellationToken cancellationToken) =>
        dbContext.CorrectionExecutions.FirstOrDefaultAsync(
            execution =>
                execution.PropertyId == propertyId &&
                execution.CaseId == caseId,
            cancellationToken);
}
