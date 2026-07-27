namespace BunkFy.Modules.Retention.Application.Ports;

using BunkFy.Modules.Retention.Domain.Aggregates;

public interface IRetentionExecutionRepository
{
    Task AddAsync(
        RetentionExecution execution,
        CancellationToken cancellationToken);

    Task<RetentionExecution?> GetAsync(
        Guid executionId,
        CancellationToken cancellationToken);
}
