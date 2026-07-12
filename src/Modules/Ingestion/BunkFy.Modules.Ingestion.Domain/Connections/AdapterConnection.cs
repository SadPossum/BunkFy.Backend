namespace BunkFy.Modules.Ingestion.Domain.Connections;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Domain.Errors;

public sealed partial class AdapterConnection : ScopedAggregateRoot<Guid>
{
    public const int AdapterTypeMaxLength = AdapterProtocolLimits.AdapterTypeMaxLength;
    public const int ReferenceMaxLength = 1024;
    public const int CheckpointMaxLength = AdapterProtocolLimits.CheckpointMaxLength;
    public const int MinimumPollingIntervalSeconds = 60;
    public const int MaximumPollingIntervalSeconds = 30 * 24 * 60 * 60;
    public const int MaximumPollingScheduleAttempts = 10;

    private AdapterConnection() { }

    private AdapterConnection(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid PropertyId { get; private set; }
    public string AdapterType { get; private set; } = string.Empty;
    public AdapterExecutionMode ExecutionMode { get; private set; }
    public IngestionConflictPolicy ConflictPolicy { get; private set; }
    public string ConfigurationReference { get; private set; } = string.Empty;
    public string? SecretReference { get; private set; }
    public string? Checkpoint { get; private set; }
    public int? PollingIntervalSeconds { get; private set; }
    public int? PollingScheduleMaxAttempts { get; private set; }
    public DateTimeOffset? PollingScheduleConfiguredAtUtc { get; private set; }
    public Guid? RemoteLeaseRunId { get; private set; }
    public Guid? RemoteLeaseId { get; private set; }
    public Guid? RemoteLeaseClaimId { get; private set; }
    public Guid? RemoteLeaseCredentialId { get; private set; }
    public Guid? RemoteLeaseWorkerId { get; private set; }
    public long RemoteLeaseEpoch { get; private set; }
    public DateTimeOffset? RemoteLeaseExpiresAtUtc { get; private set; }
    public AdapterConnectionState State { get; private set; } = AdapterConnectionState.Enabled;
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    private Result EnsureCurrentRemoteLease(
        Guid runId,
        Guid leaseId,
        long leaseEpoch,
        Guid credentialId,
        Guid workerId,
        DateTimeOffset nowUtc)
    {
        if (!this.RemoteLeaseId.HasValue || !this.RemoteLeaseRunId.HasValue ||
            !this.RemoteLeaseCredentialId.HasValue || !this.RemoteLeaseWorkerId.HasValue ||
            !this.RemoteLeaseExpiresAtUtc.HasValue)
        {
            return Result.Failure(IngestionDomainErrors.RemoteLeaseNotActive);
        }

        if (this.RemoteLeaseRunId != runId || this.RemoteLeaseId != leaseId ||
            this.RemoteLeaseEpoch != leaseEpoch || this.RemoteLeaseCredentialId != credentialId ||
            this.RemoteLeaseWorkerId != workerId)
        {
            return Result.Failure(IngestionDomainErrors.RemoteLeaseMismatch);
        }

        return this.RemoteLeaseExpiresAtUtc <= nowUtc
            ? Result.Failure(IngestionDomainErrors.RemoteLeaseExpired)
            : Result.Success();
    }

    private void ClearRemoteLease()
    {
        this.RemoteLeaseRunId = null;
        this.RemoteLeaseId = null;
        this.RemoteLeaseClaimId = null;
        this.RemoteLeaseCredentialId = null;
        this.RemoteLeaseWorkerId = null;
        this.RemoteLeaseExpiresAtUtc = null;
    }

    private Result EnsureVersion(long expectedVersion) =>
        expectedVersion == this.Version
            ? Result.Success()
            : Result.Failure(IngestionDomainErrors.VersionConflict);

    private void Touch(DateTimeOffset nowUtc)
    {
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
