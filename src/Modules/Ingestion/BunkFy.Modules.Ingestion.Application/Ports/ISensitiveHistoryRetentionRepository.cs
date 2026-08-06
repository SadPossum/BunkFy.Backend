namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface ISensitiveHistoryRetentionRepository
{
    Task<IReadOnlyList<SensitiveHistoryRedactionCandidate>>
        FindRedactionCandidatesAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);

    Task<SensitiveHistoryRedactionBatchResult> RedactSelectedAsync(
        IReadOnlyCollection<Guid> proposalIds,
        IReadOnlyCollection<Guid> dispatchIds,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public enum SensitiveHistoryRecordKind
{
    Proposal = 1,
    Dispatch = 2
}

public sealed record SensitiveHistoryRedactionCandidate(
    SensitiveHistoryRecordKind Kind,
    Guid RecordId,
    Guid ConnectionId,
    Guid SourceLinkId,
    DateTimeOffset RetainUntilUtc);

public sealed record SensitiveHistoryRedactionBatchResult(
    int ProposalCount,
    int DispatchCount)
{
    public int TotalCount => this.ProposalCount + this.DispatchCount;
}
