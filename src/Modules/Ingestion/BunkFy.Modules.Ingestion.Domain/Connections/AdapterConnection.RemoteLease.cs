namespace BunkFy.Modules.Ingestion.Domain.Connections;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Domain.Errors;

public sealed partial class AdapterConnection
{
    public Result<RemoteAdapterLeaseState> ClaimRemoteLease(
        Guid runId,
        Guid leaseId,
        Guid claimId,
        Guid credentialId,
        Guid workerId,
        TimeSpan duration,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return Result.Failure<RemoteAdapterLeaseState>(version.Error);
        }

        if (this.ExecutionMode != AdapterExecutionMode.RemotePolling)
        {
            return Result.Failure<RemoteAdapterLeaseState>(
                IngestionDomainErrors.RemoteLeaseRequiresRemotePollingMode);
        }

        if (this.State != AdapterConnectionState.Enabled)
        {
            return Result.Failure<RemoteAdapterLeaseState>(IngestionDomainErrors.ConnectionNotEnabled);
        }

        if (runId == Guid.Empty || leaseId == Guid.Empty || claimId == Guid.Empty ||
            credentialId == Guid.Empty || workerId == Guid.Empty)
        {
            return Result.Failure<RemoteAdapterLeaseState>(IngestionDomainErrors.RemoteLeaseIdentityInvalid);
        }

        if (duration < TimeSpan.FromSeconds(AdapterRemoteLeaseContractLimits.MinimumLeaseSeconds) ||
            duration > TimeSpan.FromSeconds(AdapterRemoteLeaseContractLimits.MaximumLeaseSeconds))
        {
            return Result.Failure<RemoteAdapterLeaseState>(IngestionDomainErrors.RemoteLeaseDurationInvalid);
        }

        if (this.RemoteLeaseId.HasValue && this.RemoteLeaseExpiresAtUtc > nowUtc)
        {
            return Result.Failure<RemoteAdapterLeaseState>(IngestionDomainErrors.RemoteLeaseAlreadyActive);
        }

        if (this.RemoteLeaseEpoch == long.MaxValue)
        {
            return Result.Failure<RemoteAdapterLeaseState>(IngestionDomainErrors.RemoteLeaseEpochExhausted);
        }

        this.RemoteLeaseRunId = runId;
        this.RemoteLeaseId = leaseId;
        this.RemoteLeaseClaimId = claimId;
        this.RemoteLeaseCredentialId = credentialId;
        this.RemoteLeaseWorkerId = workerId;
        this.RemoteLeaseEpoch++;
        this.RemoteLeaseExpiresAtUtc = nowUtc.Add(duration);
        this.Touch(nowUtc);
        return Result.Success(new RemoteAdapterLeaseState(
            runId,
            leaseId,
            claimId,
            this.RemoteLeaseEpoch,
            credentialId,
            workerId,
            this.RemoteLeaseExpiresAtUtc.Value));
    }

    public Result<DateTimeOffset> RenewRemoteLease(
        Guid runId,
        Guid leaseId,
        long leaseEpoch,
        Guid credentialId,
        Guid workerId,
        TimeSpan duration,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return Result.Failure<DateTimeOffset>(version.Error);
        }

        Result current = this.EnsureCurrentRemoteLease(
            runId, leaseId, leaseEpoch, credentialId, workerId, nowUtc);
        if (current.IsFailure)
        {
            return Result.Failure<DateTimeOffset>(current.Error);
        }

        if (duration < TimeSpan.FromSeconds(AdapterRemoteLeaseContractLimits.MinimumLeaseSeconds) ||
            duration > TimeSpan.FromSeconds(AdapterRemoteLeaseContractLimits.MaximumLeaseSeconds))
        {
            return Result.Failure<DateTimeOffset>(IngestionDomainErrors.RemoteLeaseDurationInvalid);
        }

        this.RemoteLeaseExpiresAtUtc = nowUtc.Add(duration);
        this.Touch(nowUtc);
        return Result.Success(this.RemoteLeaseExpiresAtUtc.Value);
    }

    public Result AuthorizeRemoteLeaseOperation(
        Guid runId,
        Guid leaseId,
        long leaseEpoch,
        Guid credentialId,
        Guid workerId,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        Result current = this.EnsureCurrentRemoteLease(
            runId, leaseId, leaseEpoch, credentialId, workerId, nowUtc);
        if (current.IsFailure)
        {
            return current;
        }

        this.Touch(nowUtc);
        return Result.Success();
    }

    public Result AdvanceRemoteCheckpoint(
        Guid runId,
        Guid leaseId,
        long leaseEpoch,
        Guid credentialId,
        Guid workerId,
        string checkpoint,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        Result current = this.EnsureCurrentRemoteLease(
            runId, leaseId, leaseEpoch, credentialId, workerId, nowUtc);
        if (current.IsFailure)
        {
            return current;
        }

        string? normalizedCheckpoint = NormalizeOptional(checkpoint);
        if (normalizedCheckpoint is null || normalizedCheckpoint.Length > CheckpointMaxLength)
        {
            return Result.Failure(IngestionDomainErrors.CheckpointInvalid);
        }

        this.Checkpoint = normalizedCheckpoint;
        this.Touch(nowUtc);
        return Result.Success();
    }

    public Result ReleaseRemoteLease(
        Guid runId,
        Guid leaseId,
        long leaseEpoch,
        Guid credentialId,
        Guid workerId,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        Result current = this.EnsureCurrentRemoteLease(
            runId, leaseId, leaseEpoch, credentialId, workerId, nowUtc);
        if (current.IsFailure)
        {
            return current;
        }

        this.ClearRemoteLease();
        this.Touch(nowUtc);
        return Result.Success();
    }

}
