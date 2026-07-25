namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;

internal sealed record PrepareDataRightsRestoreBatchCommand(
    long ExpectedCheckpointVersion,
    DataRightsRestoreCursor ExpectedCursor,
    IReadOnlyList<DataRightsLedgerDelta> Deltas)
    : ITransactionalCommand<Unit>, IDataRightsPersistenceRetryableCommand;
