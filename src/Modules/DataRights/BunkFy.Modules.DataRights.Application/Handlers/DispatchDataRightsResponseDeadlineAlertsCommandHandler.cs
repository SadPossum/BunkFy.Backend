namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class DispatchDataRightsResponseDeadlineAlertsCommandHandler(
    IDataRightsResponseDeadlineAlertRepository alerts,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock)
    : ICommandHandler<
        DispatchDataRightsResponseDeadlineAlertsCommand,
        DataRightsResponseDeadlineAlertDispatchBatchResult>
{
    internal static readonly TimeSpan DueSoonHorizon = TimeSpan.FromHours(48);

    public async Task<Result<DataRightsResponseDeadlineAlertDispatchBatchResult>>
        HandleAsync(
            DispatchDataRightsResponseDeadlineAlertsCommand command,
            CancellationToken cancellationToken)
    {
        if (command.BatchSize is <= 0 or >
            DispatchDataRightsResponseDeadlineAlertsPayload.MaximumBatchSize)
        {
            return Result.Failure<
                DataRightsResponseDeadlineAlertDispatchBatchResult>(
                    DataRightsApplicationErrors.DeadlineAlertTaskOptionsInvalid);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        DataRightsResponseDeadlineAlertClaimResult claimed = await alerts
            .ClaimAsync(
                nowUtc,
                nowUtc.Add(DueSoonHorizon),
                command.BatchSize,
                cancellationToken)
            .ConfigureAwait(false);
        IOutboxWriter outbox = outboxWriters.GetRequired(
            DataRightsModuleMetadata.Name);

        foreach (DataRightsResponseDeadlineAlertDispatch dispatch in
                 claimed.Dispatches)
        {
            await outbox.EnqueueAsync(
                new DataRightsResponseDeadlineAlertDueIntegrationEvent(
                    dispatch.DispatchId,
                    dispatch.ScopeId,
                    nowUtc,
                    dispatch.CaseId,
                    dispatch.PropertyId,
                    dispatch.AlertKind,
                    dispatch.DueAtUtc),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(
            new DataRightsResponseDeadlineAlertDispatchBatchResult(
                claimed.ProcessedCount,
                claimed.Dispatches.Count));
    }
}
