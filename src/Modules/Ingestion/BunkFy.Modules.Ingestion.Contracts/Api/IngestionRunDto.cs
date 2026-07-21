namespace BunkFy.Modules.Ingestion.Contracts;

public enum IngestionRunStatus
{
    Unknown = 0,
    Running = 1,
    Succeeded = 2,
    PartiallySucceeded = 3,
    Failed = 4,
    Cancelled = 5
}

public enum IngestionRunExecutionKindDto
{
    Unknown = 0,
    TaskRuntime = 1,
    RemoteLease = 2
}

public sealed record IngestionRunDto(
    Guid RunId,
    Guid ConnectionId,
    Guid PropertyId,
    IngestionRunExecutionKindDto ExecutionKind,
    Guid? TaskRunId,
    int? TaskAttempt,
    Guid? RemoteLeaseId,
    Guid? RemoteClaimId,
    long? RemoteLeaseEpoch,
    Guid? RemoteWorkerId,
    DateTimeOffset? RemoteLeaseExpiresAtUtc,
    string? StartingCheckpoint,
    string? AcceptedCheckpoint,
    IngestionRunStatus Status,
    int ObservedCount,
    int AcceptedCount,
    int RejectedCount,
    string? ErrorCode,
    long Version,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record IngestionRunListResponse(
    IReadOnlyCollection<IngestionRunDto> Runs,
    int Page,
    int PageSize,
    long TotalCount);
