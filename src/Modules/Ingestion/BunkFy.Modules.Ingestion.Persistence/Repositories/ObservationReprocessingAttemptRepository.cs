namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using Microsoft.EntityFrameworkCore;

internal sealed class ObservationReprocessingAttemptRepository(IngestionDbContext dbContext)
    : IObservationReprocessingAttemptRepository
{
    public Task<ObservationReprocessingAttempt?> GetAsync(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        dbContext.ObservationReprocessingAttempts.FirstOrDefaultAsync(
            attempt => attempt.Id == attemptId,
            cancellationToken);

    public Task<ObservationReprocessingAttempt?> FindActiveBySourceAsync(
        Guid sourceReceiptId,
        CancellationToken cancellationToken) =>
        dbContext.ObservationReprocessingAttempts.FirstOrDefaultAsync(
            attempt => attempt.SourceReceiptId == sourceReceiptId &&
                       (attempt.State == ObservationReprocessingState.Queued ||
                        attempt.State == ObservationReprocessingState.Running),
            cancellationToken);

    public Task AddAsync(ObservationReprocessingAttempt attempt, CancellationToken cancellationToken)
    {
        dbContext.ObservationReprocessingAttempts.Add(attempt);
        return Task.CompletedTask;
    }
}
