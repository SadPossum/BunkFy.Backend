namespace BunkFy.Modules.Ingestion.Application.Tasks;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;

internal sealed class PurgeExpiredRawPayloadsTaskHandler(
    ITaskCommandDispatcher commandDispatcher,
    IRawPayloadStore rawPayloads)
    : ITaskHandler<PurgeExpiredRawPayloadsPayload>
{
    public async Task HandleAsync(
        PurgeExpiredRawPayloadsPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (payload.BatchSize is <= 0 or > PurgeExpiredRawPayloadsPayload.MaximumBatchSize ||
            payload.MaxBatches is <= 0 or > PurgeExpiredRawPayloadsPayload.MaximumBatches ||
            payload.StaleClaimMinutes is < PurgeExpiredRawPayloadsPayload.MinimumStaleClaimMinutes or
                > PurgeExpiredRawPayloadsPayload.MaximumStaleClaimMinutes ||
            string.IsNullOrWhiteSpace(context.ScopeId))
        {
            throw new InvalidOperationException(IngestionApplicationErrors.RetentionTaskOptionsInvalid.Code);
        }

        for (int batch = 0; batch < payload.MaxBatches; batch++)
        {
            Result<IReadOnlyList<RawPayloadPurgeCandidate>> claimed = await commandDispatcher
                .DispatchAsync<ClaimExpiredRawPayloadsCommand, IReadOnlyList<RawPayloadPurgeCandidate>>(
                    context,
                    new ClaimExpiredRawPayloadsCommand(
                        context.RunId,
                        payload.BatchSize,
                        payload.StaleClaimMinutes),
                    cancellationToken).ConfigureAwait(false);
            if (claimed.IsFailure)
            {
                throw new InvalidOperationException($"{claimed.Error.Code}: {claimed.Error.Message}");
            }

            foreach (RawPayloadPurgeCandidate candidate in claimed.Value)
            {
                _ = await rawPayloads.DeleteAsync(
                    candidate.RawPayloadFileId,
                    context.ScopeId,
                    candidate.ConnectionId,
                    cancellationToken).ConfigureAwait(false);
                Result<Unit> completed = await commandDispatcher.DispatchAsync<CompleteRawPayloadPurgeCommand, Unit>(
                    context,
                    new CompleteRawPayloadPurgeCommand(candidate.ReceiptId, context.RunId),
                    cancellationToken).ConfigureAwait(false);
                if (completed.IsFailure)
                {
                    throw new InvalidOperationException($"{completed.Error.Code}: {completed.Error.Message}");
                }
            }

            if (claimed.Value.Count < payload.BatchSize)
            {
                return;
            }
        }
    }
}
