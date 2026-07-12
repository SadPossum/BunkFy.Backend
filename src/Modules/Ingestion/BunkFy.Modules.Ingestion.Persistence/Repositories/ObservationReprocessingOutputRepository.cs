namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using Microsoft.EntityFrameworkCore;

internal sealed class ObservationReprocessingOutputRepository(IngestionDbContext dbContext)
    : IObservationReprocessingOutputRepository
{
    public Task<ObservationReprocessingOutput?> GetAsync(
        Guid attemptId,
        int outputIndex,
        CancellationToken cancellationToken) =>
        dbContext.ObservationReprocessingOutputs.FirstOrDefaultAsync(
            output => output.AttemptId == attemptId && output.OutputIndex == outputIndex,
            cancellationToken);

    public Task AddAsync(ObservationReprocessingOutput output, CancellationToken cancellationToken)
    {
        dbContext.ObservationReprocessingOutputs.Add(output);
        return Task.CompletedTask;
    }
}
