namespace BunkFy.Modules.Ingestion.Contracts;

public sealed record AdapterIngressTenantControlDto(
    bool IsSuspended,
    string? LastReasonCode,
    string? LastChangedBy,
    DateTimeOffset? LastChangedAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    DateTimeOffset? ResumedAtUtc,
    long Version);

public sealed record AdapterIngressGlobalControlDto(
    bool IsStopped,
    string? LastReasonCode,
    string? LastChangedBy,
    DateTimeOffset? LastChangedAtUtc,
    DateTimeOffset? StoppedAtUtc,
    DateTimeOffset? ResumedAtUtc,
    long Version);
