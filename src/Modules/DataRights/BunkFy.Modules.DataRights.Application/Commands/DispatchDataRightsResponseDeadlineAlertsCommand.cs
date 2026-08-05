namespace BunkFy.Modules.DataRights.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record DispatchDataRightsResponseDeadlineAlertsCommand(int BatchSize)
    : ITransactionalCommand<DataRightsResponseDeadlineAlertDispatchBatchResult>;

public sealed record DataRightsResponseDeadlineAlertDispatchBatchResult(
    int ProcessedCount,
    int DispatchedCount);
