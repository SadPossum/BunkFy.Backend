namespace BunkFy.Modules.Ingestion.Application.Commands;

internal sealed record IngestionAnonymisationExecutionProof(
    int ReceiptContractVersion,
    Guid ReceiptId,
    long ResultingSourceLinkVersion,
    string ReceiptSha256,
    DateTimeOffset CompletedAtUtc);
