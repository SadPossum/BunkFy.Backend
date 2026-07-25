namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;

internal sealed record AdvanceDataRightsRestoreCheckpointCommand(
    long ExpectedCheckpointVersion,
    DataRightsRestoreCursor ExpectedCursor,
    DataRightsLedgerDeltaCursor NextCursor,
    IReadOnlyList<DataRightsRestoreOwnerProofBinding> OwnerProofs)
    : ITransactionalCommand<DataRightsRestoreCheckpointState>,
        IDataRightsPersistenceRetryableCommand;
