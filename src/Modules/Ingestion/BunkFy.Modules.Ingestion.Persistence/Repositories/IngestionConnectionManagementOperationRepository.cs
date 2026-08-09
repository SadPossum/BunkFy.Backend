namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionConnectionManagementOperationRepository(
    IngestionDbContext dbContext)
    : IIngestionConnectionManagementOperationRepository
{
    public async Task<IngestionConnectionManagementOperationRecord?> GetAsync(
        Guid connectionId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        IngestionConnectionManagementOperation? operation =
            await dbContext.ConnectionManagementOperations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.ConnectionId == connectionId &&
                        candidate.Id == operationId,
                    cancellationToken)
                .ConfigureAwait(false);
        return operation?.ToRecord();
    }

    public Task AddAsync(
        IngestionConnectionManagementOperationRecord operation,
        CancellationToken cancellationToken)
    {
        dbContext.ConnectionManagementOperations.Add(
            new IngestionConnectionManagementOperation(operation));
        return Task.CompletedTask;
    }
}
