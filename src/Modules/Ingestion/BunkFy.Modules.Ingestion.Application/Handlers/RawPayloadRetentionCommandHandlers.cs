namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Retention;

internal sealed class ClaimExpiredRawPayloadsCommandHandler(
    IRawPayloadRetentionRepository retention,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<ClaimExpiredRawPayloadsCommand, IReadOnlyList<RawPayloadPurgeCandidate>>
{
    public async Task<Result<IReadOnlyList<RawPayloadPurgeCandidate>>> HandleAsync(
        ClaimExpiredRawPayloadsCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<IReadOnlyList<RawPayloadPurgeCandidate>>(IngestionApplicationErrors.ScopeRequired);
        }

        if (command.ClaimId == Guid.Empty ||
            command.BatchSize is <= 0 or > PurgeExpiredRawPayloadsPayload.MaximumBatchSize ||
            command.StaleClaimMinutes is < PurgeExpiredRawPayloadsPayload.MinimumStaleClaimMinutes or
                > PurgeExpiredRawPayloadsPayload.MaximumStaleClaimMinutes)
        {
            return Result.Failure<IReadOnlyList<RawPayloadPurgeCandidate>>(
                IngestionApplicationErrors.RetentionTaskOptionsInvalid);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        return Result.Success(await retention.ClaimBatchAsync(
            command.ClaimId,
            nowUtc,
            nowUtc.AddMinutes(-command.StaleClaimMinutes),
            command.BatchSize,
            cancellationToken).ConfigureAwait(false));
    }
}

internal sealed class CompleteRawPayloadPurgeCommandHandler(
    IObservationReceiptRepository receipts,
    IIngestionRetentionExecutionRepository executions,
    ISystemClock clock)
    : ICommandHandler<CompleteRawPayloadPurgeCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        CompleteRawPayloadPurgeCommand command,
        CancellationToken cancellationToken)
    {
        ObservationReceipt? receipt = await receipts.GetAsync(command.ReceiptId, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is null)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.ReceiptNotFound);
        }

        Result completed = receipt.CompleteRawPayloadPurge(command.ClaimId, clock.UtcNow);
        if (completed.IsFailure)
        {
            return Result.Failure<Unit>(completed.Error);
        }

        if (command.RetentionExecutionId is { } executionId)
        {
            IngestionRetentionExecution? execution = await executions.GetAsync(
                executionId,
                cancellationToken).ConfigureAwait(false);
            if (execution is null)
            {
                return Result.Failure<Unit>(
                    IngestionApplicationErrors.RetentionExecutionNotFound);
            }

            Result recorded = execution.RecordAffected(1);
            if (recorded.IsFailure)
            {
                return Result.Failure<Unit>(recorded.Error);
            }
        }

        return Result.Success(Unit.Value);
    }
}
