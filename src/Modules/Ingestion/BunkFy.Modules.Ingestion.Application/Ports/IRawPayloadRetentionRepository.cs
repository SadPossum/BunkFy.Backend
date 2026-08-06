namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface IRawPayloadRetentionRepository
{
    Task<IReadOnlyList<RawPayloadPurgeClaimCandidate>> FindClaimCandidatesAsync(
        Guid claimId,
        DateTimeOffset nowUtc,
        DateTimeOffset staleClaimBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RawPayloadPurgeCandidate>> ClaimSelectedAsync(
        IReadOnlyCollection<Guid> receiptIds,
        Guid claimId,
        DateTimeOffset nowUtc,
        DateTimeOffset staleClaimBeforeUtc,
        CancellationToken cancellationToken);
}

public sealed record RawPayloadPurgeClaimCandidate(
    Guid ReceiptId,
    Guid ConnectionId,
    Guid SourceLinkId);

public sealed record RawPayloadPurgeCandidate(
    Guid ReceiptId,
    Guid RawPayloadFileId,
    Guid ConnectionId);
