namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Runs;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionRunRepository(IngestionDbContext dbContext) : IIngestionRunRepository
{
    public Task<IngestionRun?> GetAsync(Guid runId, CancellationToken cancellationToken) =>
        dbContext.Runs.FirstOrDefaultAsync(run => run.Id == runId, cancellationToken);

    public Task<IngestionRun?> FindByTaskExecutionAsync(
        Guid taskRunId,
        int taskAttempt,
        CancellationToken cancellationToken) =>
        dbContext.Runs.FirstOrDefaultAsync(
            run => run.TaskRunId == taskRunId && run.TaskAttempt == taskAttempt,
            cancellationToken);

    public Task<Guid?> FindByTaskExecutionIdAsync(
        Guid taskRunId,
        int taskAttempt,
        CancellationToken cancellationToken) =>
        dbContext.Runs
            .AsNoTracking()
            .Where(run =>
                run.TaskRunId == taskRunId &&
                run.TaskAttempt == taskAttempt)
            .Select(run => (Guid?)run.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<IngestionRun?> FindActiveByConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken) =>
        dbContext.Runs.FirstOrDefaultAsync(
            run => run.ConnectionId == connectionId && run.State == IngestionRunState.Running,
            cancellationToken);

    public Task<Guid?> FindActiveIdByConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken) =>
        dbContext.Runs
            .AsNoTracking()
            .Where(run =>
                run.ConnectionId == connectionId &&
                run.State == IngestionRunState.Running)
            .Select(run => (Guid?)run.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task AddAsync(IngestionRun run, CancellationToken cancellationToken)
    {
        dbContext.Runs.Add(run);
        return Task.CompletedTask;
    }
}
