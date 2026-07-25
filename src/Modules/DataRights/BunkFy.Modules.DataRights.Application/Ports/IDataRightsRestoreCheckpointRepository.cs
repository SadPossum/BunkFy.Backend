namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Entities;

public interface IDataRightsRestoreCheckpointRepository
{
    Task<DataRightsRestoreCheckpoint?> GetAsync(
        CancellationToken cancellationToken);

    Task AddAsync(
        DataRightsRestoreCheckpoint checkpoint,
        CancellationToken cancellationToken);
}
