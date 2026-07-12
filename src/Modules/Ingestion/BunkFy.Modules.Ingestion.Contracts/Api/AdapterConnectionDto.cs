namespace BunkFy.Modules.Ingestion.Contracts;

using BunkFy.Adapter.Abstractions;

public enum AdapterConnectionStatus
{
    Unknown = 0,
    Enabled = 1,
    Disabled = 2
}

public enum AdapterConflictPolicy
{
    Unknown = 0,
    SuggestionsOnly = 1,
    AutoApplyWhenAdapterBaselineUnchanged = 2
}

public sealed record AdapterConnectionDto(
    Guid ConnectionId,
    Guid PropertyId,
    string AdapterType,
    AdapterExecutionMode ExecutionMode,
    int? PollingIntervalSeconds,
    int? PollingScheduleMaxAttempts,
    DateTimeOffset? PollingScheduleConfiguredAtUtc,
    AdapterConflictPolicy ConflictPolicy,
    string ConfigurationReference,
    bool HasSecretReference,
    string? Checkpoint,
    AdapterConnectionStatus Status,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record AdapterConnectionListResponse(
    IReadOnlyCollection<AdapterConnectionDto> Connections,
    int Page,
    int PageSize,
    long TotalCount);
