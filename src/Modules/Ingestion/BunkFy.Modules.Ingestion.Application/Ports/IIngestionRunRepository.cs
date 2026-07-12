namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Runs;

public interface IIngestionRunRepository
{
    Task<IngestionRun?> GetAsync(Guid runId, CancellationToken cancellationToken);
    Task<IngestionRun?> FindByTaskExecutionAsync(
        Guid taskRunId,
        int taskAttempt,
        CancellationToken cancellationToken);
    Task<IngestionRun?> FindActiveByConnectionAsync(Guid connectionId, CancellationToken cancellationToken);
    Task AddAsync(IngestionRun run, CancellationToken cancellationToken);
}
