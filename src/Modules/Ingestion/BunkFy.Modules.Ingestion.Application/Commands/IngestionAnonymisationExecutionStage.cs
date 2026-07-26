namespace BunkFy.Modules.Ingestion.Application.Commands;

internal sealed record IngestionAnonymisationExecutionStage(
    Guid TombstoneId,
    IReadOnlyCollection<IngestionRawPayloadDeletion> RawPayloads,
    IngestionAnonymisationExecutionProof? CompletedProof);
