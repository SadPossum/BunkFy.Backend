namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetDataRightsRestoreCheckpointQueryHandler(
    IDataRightsRestoreCheckpointRepository checkpoints)
    : IQueryHandler<
        GetDataRightsRestoreCheckpointQuery,
        DataRightsRestoreCheckpointState>
{
    public async Task<Result<DataRightsRestoreCheckpointState>> HandleAsync(
        GetDataRightsRestoreCheckpointQuery query,
        CancellationToken cancellationToken)
    {
        DataRightsRestoreCheckpoint? checkpoint =
            await checkpoints.GetAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(checkpoint is null
            ? new DataRightsRestoreCheckpointState(
                Version: 0,
                DataRightsRestoreCursor.Genesis,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256)
            : new DataRightsRestoreCheckpointState(
                checkpoint.Version,
                checkpoint.Cursor,
                checkpoint.StorageMacSha256));
    }
}
