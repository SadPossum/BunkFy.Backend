namespace BunkFy.Modules.Ingestion.Application.Commands;

internal sealed record IngestionRawPayloadDeletion(
    Guid FileId,
    Guid ConnectionId);
