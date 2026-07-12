namespace BunkFy.Modules.Ingestion.Contracts;

public enum ObservationReceiptStatus
{
    Unknown = 0,
    Pending = 1,
    Processed = 2,
    Rejected = 3
}

public enum RawPayloadRetentionStatus
{
    Unknown = 0,
    Available = 1,
    Purging = 2,
    Purged = 3
}

public sealed record ObservationReceiptDto(
    Guid ReceiptId,
    Guid PropertyId,
    Guid ConnectionId,
    Guid? RunId,
    Guid OperationId,
    string SourceRecordType,
    string ExternalId,
    string? SourceRevision,
    string ContentHash,
    Guid RawPayloadFileId,
    RawPayloadRetentionStatus RawPayloadStatus,
    DateTimeOffset RawPayloadRetainUntilUtc,
    DateTimeOffset? RawPayloadPurgedAtUtc,
    Guid? ActiveReprocessingAttemptId,
    DateTimeOffset? ReprocessingReservationExpiresAtUtc,
    Guid? SourceReceiptId,
    Guid? ReprocessingAttemptId,
    string? ParserType,
    int? ParserVersion,
    int? ParserOutputIndex,
    DateTimeOffset? SourceUpdatedAtUtc,
    DateTimeOffset ObservedAtUtc,
    ObservationReceiptStatus Status,
    string? RejectionReason,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? ProcessedAtUtc);

public sealed record ObservationReceiptListResponse(
    IReadOnlyCollection<ObservationReceiptDto> Receipts,
    int Page,
    int PageSize,
    long TotalCount);
