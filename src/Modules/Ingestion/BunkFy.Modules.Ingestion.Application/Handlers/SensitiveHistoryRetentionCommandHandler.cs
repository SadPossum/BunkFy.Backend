namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;

internal sealed class RedactExpiredSensitiveHistoryCommandHandler(
    ISensitiveHistoryRetentionRepository retention,
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

        return Result.Success(await retention.RedactBatchAsync(
            clock.UtcNow,
            command.BatchSize,
            cancellationToken).ConfigureAwait(false));
    }
}
