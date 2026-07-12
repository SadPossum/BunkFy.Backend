namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface ISensitiveHistoryRetentionRepository
{
    Task<SensitiveHistoryRedactionBatchResult> RedactBatchAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed record SensitiveHistoryRedactionBatchResult(
    int ProposalCount,
    int DispatchCount)
{
    public int TotalCount => this.ProposalCount + this.DispatchCount;
}
