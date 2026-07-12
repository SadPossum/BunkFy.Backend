namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Reprocessing;

public interface IObservationReprocessingOutputRepository
{
    Task<ObservationReprocessingOutput?> GetAsync(
        Guid attemptId,
        int outputIndex,
        CancellationToken cancellationToken);

    Task AddAsync(ObservationReprocessingOutput output, CancellationToken cancellationToken);
}
