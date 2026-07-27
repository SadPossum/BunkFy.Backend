namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Retention;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionRetentionExecutionRepository(
    IngestionDbContext dbContext)
    : IIngestionRetentionExecutionRepository
{
    public Task AddAsync(
        IngestionRetentionExecution execution,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionExecutions.Add(execution);
        return Task.CompletedTask;
    }

    public Task<IngestionRetentionExecution?> GetAsync(
        Guid executionId,
        CancellationToken cancellationToken) =>
        dbContext.RetentionExecutions.SingleOrDefaultAsync(
            execution => execution.Id == executionId,
            cancellationToken);
}
