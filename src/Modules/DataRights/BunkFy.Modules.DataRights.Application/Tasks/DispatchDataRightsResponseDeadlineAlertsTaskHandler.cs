namespace BunkFy.Modules.DataRights.Application.Tasks;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;

internal sealed class DispatchDataRightsResponseDeadlineAlertsTaskHandler(
    ITaskCommandDispatcher commandDispatcher)
    : ITaskHandler<DispatchDataRightsResponseDeadlineAlertsPayload>
{
    public async Task HandleAsync(
        DispatchDataRightsResponseDeadlineAlertsPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (payload.BatchSize is <= 0 or >
                DispatchDataRightsResponseDeadlineAlertsPayload.MaximumBatchSize ||
            payload.MaxBatches is <= 0 or >
                DispatchDataRightsResponseDeadlineAlertsPayload.MaximumBatches ||
            string.IsNullOrWhiteSpace(context.ScopeId))
        {
            throw new InvalidOperationException(
                DataRightsApplicationErrors.DeadlineAlertTaskOptionsInvalid.Code);
        }

        for (int batch = 0; batch < payload.MaxBatches; batch++)
        {
            Result<DataRightsResponseDeadlineAlertDispatchBatchResult> dispatched =
                await commandDispatcher.DispatchAsync<
                        DispatchDataRightsResponseDeadlineAlertsCommand,
                        DataRightsResponseDeadlineAlertDispatchBatchResult>(
                    context,
                    new(payload.BatchSize),
                    cancellationToken).ConfigureAwait(false);
            if (dispatched.IsFailure)
            {
                throw new InvalidOperationException(
                    $"{dispatched.Error.Code}: {dispatched.Error.Message}");
            }

            if (dispatched.Value.ProcessedCount < payload.BatchSize)
            {
                return;
            }
        }
    }
}
