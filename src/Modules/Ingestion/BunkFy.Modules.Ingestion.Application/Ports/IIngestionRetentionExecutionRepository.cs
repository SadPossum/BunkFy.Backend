namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Retention;

public interface IIngestionRetentionExecutionRepository
{
    Task AddAsync(
        IngestionRetentionExecution execution,
        CancellationToken cancellationToken);

    Task<IngestionRetentionExecution?> GetAsync(
        Guid executionId,
        CancellationToken cancellationToken);
}

public interface IIngestionRetentionStatusReader
{
    Task<IngestionRetentionBacklog> ReadRawPayloadBacklogAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<IngestionRetentionBacklog> ReadSensitiveHistoryBacklogAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public sealed record IngestionRetentionBacklog(
    int EligibleCount,
    int BlockedCount,
    DateTimeOffset? EarliestBlockingHoldPlacedAtUtc);
