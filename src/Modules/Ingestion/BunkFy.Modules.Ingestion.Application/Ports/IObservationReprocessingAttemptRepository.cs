namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Reprocessing;

public interface IObservationReprocessingAttemptRepository
{
    Task<ObservationReprocessingAttempt?> GetAsync(Guid attemptId, CancellationToken cancellationToken);

    Task<ObservationReprocessingAttempt?> FindActiveBySourceAsync(
        Guid sourceReceiptId,
        CancellationToken cancellationToken);

    Task AddAsync(ObservationReprocessingAttempt attempt, CancellationToken cancellationToken);
}
