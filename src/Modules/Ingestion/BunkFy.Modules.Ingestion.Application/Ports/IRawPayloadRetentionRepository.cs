namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface IRawPayloadRetentionRepository
{
    Task<IReadOnlyList<RawPayloadPurgeCandidate>> ClaimBatchAsync(
        Guid claimId,
        DateTimeOffset nowUtc,
        DateTimeOffset staleClaimBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed record RawPayloadPurgeCandidate(
    Guid ReceiptId,
    Guid RawPayloadFileId,
    Guid ConnectionId);
