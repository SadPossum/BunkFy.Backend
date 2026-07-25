namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using Gma.Framework.Cqrs;

internal sealed record ConfirmDataRightsRestoreCheckpointCommand(
    long ExpectedCheckpointVersion,
    DataRightsLedgerDeltaCheckpoint TrustedCheckpoint,
    string ScopeSnapshotSha256,
    DateTimeOffset ReconciledAtUtc)
    : ITransactionalCommand<DataRightsRestoreCheckpointState>,
        IDataRightsPersistenceRetryableCommand;
