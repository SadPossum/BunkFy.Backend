namespace BunkFy.Modules.Ingestion.Application.Commands;

internal sealed record IngestionAnonymisationRestoreStage(
    Guid TombstoneId,
    IReadOnlyList<IngestionRawPayloadDeletion> RawPayloads);
