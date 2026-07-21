namespace BunkFy.Modules.Ingestion.Contracts;

using BunkFy.Adapter.Abstractions;

public enum AdapterConnectionOperationalState
{
    Unknown = 0,
    Disabled = 1,
    NoActivity = 2,
    RunActive = 3,
    LastRunSucceeded = 4,
    LastRunPartiallySucceeded = 5,
    LastRunFailed = 6,
    LastRunCancelled = 7,
    ObservationsReceived = 8
}

public enum AdapterCapabilityStatus
{
    Unknown = 0,
    Available = 1,
    AdapterTypeNotRegistered = 2,
    ExecutionModeUnsupported = 3
}

public sealed record AdapterConnectionHealthDto(
    Guid ConnectionId,
    Guid PropertyId,
    string AdapterType,
    AdapterConnectionStatus ConnectionStatus,
    AdapterExecutionMode ExecutionMode,
    AdapterCapabilityStatus CapabilityStatus,
    int? ProtocolVersion,
    int? ConfigurationSchemaVersion,
    int? PollingIntervalSeconds,
    int? PollingScheduleMaxAttempts,
    DateTimeOffset? PollingScheduleConfiguredAtUtc,
    DateTimeOffset? NextRunExpectedAtUtc,
    bool RunExpected,
    AdapterConnectionOperationalState OperationalState,
    Guid? LatestRunId,
    IngestionRunStatus? LatestRunStatus,
    DateTimeOffset? LatestRunStartedAtUtc,
    DateTimeOffset? LatestRunCompletedAtUtc,
    string? LatestRunErrorCode,
    DateTimeOffset? LastSuccessfulRunAtUtc,
    DateTimeOffset? LastObservationReceivedAtUtc,
    long PendingReceiptCount,
    long RejectedReceiptCount,
    long ExpiredRawPayloadCount,
    long ProtectedRawPayloadCount,
    long HeldExpiredRawPayloadCount,
    long PurgingRawPayloadCount,
    long DueSensitiveHistoryCount,
    long HeldDueSensitiveHistoryCount,
    long RedactedSensitiveHistoryCount,
    long ActiveLegalHoldCount,
    DateTimeOffset EvaluatedAtUtc);
