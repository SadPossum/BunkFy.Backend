namespace BunkFy.Modules.Ingestion.Contracts;

public enum ObservationReprocessingStatus
{
    Unknown = 0,
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    NoMatch = 4,
    Failed = 5,
    Canceled = 6,
    Expired = 7
}

public enum ObservationReprocessingOutputStatus
{
    Unknown = 0,
    Accepted = 1,
    Duplicate = 2,
    Rejected = 3
}

public sealed record ObservationParserCapabilityDto(
    string ParserType,
    int ParserVersion,
    IReadOnlyCollection<string> SupportedAdapterTypes,
    IReadOnlyCollection<string> SupportedSourceRecordTypes,
    IReadOnlyCollection<string> OutputRecordTypes);

public sealed record ObservationParserCapabilityListResponse(
    IReadOnlyCollection<ObservationParserCapabilityDto> Parsers);

public sealed record ObservationReprocessingAttemptDto(
    Guid AttemptId,
    Guid PropertyId,
    Guid ConnectionId,
    Guid SourceReceiptId,
    Guid TaskRunId,
    string ParserType,
    int ParserVersion,
    string RequestedBy,
    ObservationReprocessingStatus Status,
    int LastTaskAttempt,
    int ParsedCount,
    int AcceptedCount,
    int DuplicateCount,
    int RejectedCount,
    string? LastErrorCode,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset ReservationExpiresAtUtc,
    long Version);

public sealed record ObservationReprocessingOutputDto(
    int OutputIndex,
    Guid OperationId,
    Guid? ReceiptId,
    ObservationReprocessingOutputStatus Status,
    string RecordType,
    string ExternalId,
    string? SourceRevision,
    string ContentHash,
    string? ErrorCode,
    DateTimeOffset RecordedAtUtc);

public sealed record ObservationReprocessingAttemptDetailsDto(
    ObservationReprocessingAttemptDto Attempt,
    IReadOnlyCollection<ObservationReprocessingOutputDto> Outputs);

public sealed record ObservationReprocessingAttemptListResponse(
    IReadOnlyCollection<ObservationReprocessingAttemptDto> Attempts,
    int Page,
    int PageSize,
    long TotalCount);
