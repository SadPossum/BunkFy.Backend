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

        SensitiveHistoryRedactionBatchResult result =
            await retention.RedactBatchAsync(
                clock.UtcNow,
                command.BatchSize,
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
