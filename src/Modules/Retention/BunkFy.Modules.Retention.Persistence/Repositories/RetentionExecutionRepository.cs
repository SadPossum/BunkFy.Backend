namespace BunkFy.Modules.Retention.Persistence.Repositories;

using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class RetentionExecutionRepository(RetentionDbContext dbContext)
    : IRetentionExecutionRepository
{
    public Task AddAsync(
        RetentionExecution execution,
        CancellationToken cancellationToken)
    {
        dbContext.Executions.Add(execution);
        return Task.CompletedTask;
    }

    public Task<RetentionExecution?> GetAsync(
        Guid executionId,
        CancellationToken cancellationToken) =>
        dbContext.Executions.SingleOrDefaultAsync(
            execution => execution.Id == executionId,
            cancellationToken);
}
