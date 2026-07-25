namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsRestoreCheckpointRepository(
    DataRightsDbContext dbContext)
    : IDataRightsRestoreCheckpointRepository
{
    public Task<DataRightsRestoreCheckpoint?> GetAsync(
        CancellationToken cancellationToken) =>
        dbContext.RestoreCheckpoints.SingleOrDefaultAsync(cancellationToken);

    public Task AddAsync(
        DataRightsRestoreCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        dbContext.RestoreCheckpoints.Add(checkpoint);
        return Task.CompletedTask;
    }
}
