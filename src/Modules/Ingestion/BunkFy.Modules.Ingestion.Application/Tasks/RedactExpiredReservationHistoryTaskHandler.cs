namespace BunkFy.Modules.Ingestion.Application.Tasks;

using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;

internal sealed class RedactExpiredReservationHistoryTaskHandler(ITaskCommandDispatcher commandDispatcher)
    : ITaskHandler<RedactExpiredReservationHistoryPayload>
{
    public async Task HandleAsync(
        RedactExpiredReservationHistoryPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (payload.BatchSize is <= 0 or > RedactExpiredReservationHistoryPayload.MaximumBatchSize ||
            payload.MaxBatches is <= 0 or > RedactExpiredReservationHistoryPayload.MaximumBatches ||
            string.IsNullOrWhiteSpace(context.ScopeId))
        {
            throw new InvalidOperationException(IngestionApplicationErrors.RetentionTaskOptionsInvalid.Code);
        }

        for (int batch = 0; batch < payload.MaxBatches; batch++)
        {
            Result<SensitiveHistoryRedactionBatchResult> redacted = await commandDispatcher.DispatchAsync<
                RedactExpiredSensitiveHistoryCommand,
                SensitiveHistoryRedactionBatchResult>(
                context,
                new RedactExpiredSensitiveHistoryCommand(payload.BatchSize),
                cancellationToken).ConfigureAwait(false);
            if (redacted.IsFailure)
            {
                throw new InvalidOperationException($"{redacted.Error.Code}: {redacted.Error.Message}");
            }

            if (redacted.Value.TotalCount < payload.BatchSize)
            {
                return;
            }
        }
    }
}
