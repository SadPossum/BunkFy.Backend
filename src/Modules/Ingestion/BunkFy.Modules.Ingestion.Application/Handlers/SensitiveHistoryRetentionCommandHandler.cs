namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.Ingestion.Domain.Retention;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;

internal sealed class RedactExpiredSensitiveHistoryCommandHandler(
    ISensitiveHistoryRetentionRepository retention,
    IIngestionRetentionExecutionRepository executions,
    IngestionSourceMutationCoordinator sourceMutations,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<RedactExpiredSensitiveHistoryCommand, SensitiveHistoryRedactionBatchResult>
{
    public async Task<Result<SensitiveHistoryRedactionBatchResult>> HandleAsync(
        RedactExpiredSensitiveHistoryCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<SensitiveHistoryRedactionBatchResult>(IngestionApplicationErrors.ScopeRequired);
        }

        if (command.BatchSize is <= 0 or > RedactExpiredReservationHistoryPayload.MaximumBatchSize)
        {
            return Result.Failure<SensitiveHistoryRedactionBatchResult>(
                IngestionApplicationErrors.RetentionTaskOptionsInvalid);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        IReadOnlyList<SensitiveHistoryRedactionCandidate> candidates =
            await retention.FindRedactionCandidatesAsync(
                nowUtc,
                command.BatchSize,
                cancellationToken).ConfigureAwait(false);
        await sourceMutations.AcquireAllAsync(
                candidates
                    .Select(candidate => new IngestionSourceGraphCoordinate(
                        candidate.RecordId,
                        candidate.ConnectionId,
                        candidate.SourceLinkId))
                    .ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        SensitiveHistoryRedactionBatchResult result =
            await retention.RedactSelectedAsync(
                candidates
                    .Where(candidate =>
                        candidate.Kind == SensitiveHistoryRecordKind.Proposal)
                    .Select(candidate => candidate.RecordId)
                    .ToArray(),
                candidates
                    .Where(candidate =>
                        candidate.Kind == SensitiveHistoryRecordKind.Dispatch)
                    .Select(candidate => candidate.RecordId)
                    .ToArray(),
                nowUtc,
                cancellationToken).ConfigureAwait(false);
        if (command.RetentionExecutionId is { } executionId &&
            result.TotalCount > 0)
        {
            IngestionRetentionExecution? execution =
                await executions.GetAsync(
                    executionId,
                    cancellationToken).ConfigureAwait(false);
            if (execution is null)
            {
                return Result.Failure<SensitiveHistoryRedactionBatchResult>(
                    IngestionApplicationErrors.RetentionExecutionNotFound);
            }

            Result recorded = execution.RecordAffected(result.TotalCount);
            if (recorded.IsFailure)
            {
                return Result.Failure<SensitiveHistoryRedactionBatchResult>(
                    recorded.Error);
            }
        }

        return Result.Success(result);
    }
}
