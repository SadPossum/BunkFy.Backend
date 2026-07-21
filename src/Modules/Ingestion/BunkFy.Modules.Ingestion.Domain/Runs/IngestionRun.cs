namespace BunkFy.Modules.Ingestion.Domain.Runs;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Domain.Errors;

public sealed class IngestionRun : ScopedAggregateRoot<Guid>
{
    public const int CheckpointMaxLength = AdapterProtocolLimits.CheckpointMaxLength;
    public const int ErrorCodeMaxLength = AdapterProtocolLimits.ErrorCodeMaxLength;

    private IngestionRun() { }

    private IngestionRun(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid ConnectionId { get; private set; }
    public Guid PropertyId { get; private set; }
    public IngestionRunExecutionKind ExecutionKind { get; private set; }
    public Guid? TaskRunId { get; private set; }
    public int? TaskAttempt { get; private set; }
    public Guid? RemoteLeaseId { get; private set; }
    public Guid? RemoteClaimId { get; private set; }
    public long? RemoteLeaseEpoch { get; private set; }
    public Guid? RemoteCredentialId { get; private set; }
    public Guid? RemoteWorkerId { get; private set; }
    public DateTimeOffset? RemoteLeaseExpiresAtUtc { get; private set; }
    public string? StartingCheckpoint { get; private set; }
    public string? AcceptedCheckpoint { get; private set; }
    public IngestionRunState State { get; private set; } = IngestionRunState.Running;
    public int ObservedCount { get; private set; }
    public int AcceptedCount { get; private set; }
    public int RejectedCount { get; private set; }
    public string? ErrorCode { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static Result<IngestionRun> Start(
        Guid runId,
        string scopeId,
        Guid connectionId,
        Guid propertyId,
        Guid taskRunId,
        int taskAttempt,
        string? startingCheckpoint,
        DateTimeOffset nowUtc)
    {
        if (runId == Guid.Empty)
        {
            return Result.Failure<IngestionRun>(IngestionDomainErrors.IdRequired);
        }

        if (connectionId == Guid.Empty)
        {
            return Result.Failure<IngestionRun>(IngestionDomainErrors.ConnectionIdRequired);
        }

        if (propertyId == Guid.Empty)
        {
            return Result.Failure<IngestionRun>(IngestionDomainErrors.PropertyIdRequired);
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<IngestionRun>(IngestionDomainErrors.ScopeRequired);
        }

        if (taskRunId == Guid.Empty || taskAttempt <= 0)
        {
            return Result.Failure<IngestionRun>(IngestionDomainErrors.TaskExecutionInvalid);
        }

        Result checkpoint = ValidateCheckpoint(startingCheckpoint);
        if (checkpoint.IsFailure)
        {
            return Result.Failure<IngestionRun>(checkpoint.Error);
        }

        return Result.Success(new IngestionRun(runId, scopeId.Trim())
        {
            ConnectionId = connectionId,
            PropertyId = propertyId,
            ExecutionKind = IngestionRunExecutionKind.TaskRuntime,
            TaskRunId = taskRunId,
            TaskAttempt = taskAttempt,
            StartingCheckpoint = NormalizeCheckpoint(startingCheckpoint),
            StartedAtUtc = nowUtc
        });
    }

    public static Result<IngestionRun> StartRemote(
        Guid runId,
        string scopeId,
        Guid connectionId,
        Guid propertyId,
        Guid leaseId,
        Guid claimId,
        long leaseEpoch,
        Guid credentialId,
        Guid workerId,
        string? startingCheckpoint,
        DateTimeOffset leaseExpiresAtUtc,
        DateTimeOffset nowUtc)
    {
        if (runId == Guid.Empty || connectionId == Guid.Empty || propertyId == Guid.Empty ||
            leaseId == Guid.Empty || claimId == Guid.Empty || leaseEpoch <= 0 ||
            credentialId == Guid.Empty || workerId == Guid.Empty ||
            string.IsNullOrWhiteSpace(scopeId) || leaseExpiresAtUtc <= nowUtc)
        {
            return Result.Failure<IngestionRun>(IngestionDomainErrors.RemoteLeaseIdentityInvalid);
        }

        Result checkpoint = ValidateCheckpoint(startingCheckpoint);
        if (checkpoint.IsFailure)
        {
            return Result.Failure<IngestionRun>(checkpoint.Error);
        }

        return Result.Success(new IngestionRun(runId, scopeId.Trim())
        {
            ConnectionId = connectionId,
            PropertyId = propertyId,
            ExecutionKind = IngestionRunExecutionKind.RemoteLease,
            RemoteLeaseId = leaseId,
            RemoteClaimId = claimId,
            RemoteLeaseEpoch = leaseEpoch,
            RemoteCredentialId = credentialId,
            RemoteWorkerId = workerId,
            RemoteLeaseExpiresAtUtc = leaseExpiresAtUtc,
            StartingCheckpoint = NormalizeCheckpoint(startingCheckpoint),
            StartedAtUtc = nowUtc
        });
    }

    public Result RenewRemoteLease(
        Guid leaseId,
        long leaseEpoch,
        Guid credentialId,
        Guid workerId,
        DateTimeOffset leaseExpiresAtUtc,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result current = this.EnsureRemoteLease(
            leaseId, leaseEpoch, credentialId, workerId, expectedVersion, nowUtc);
        if (current.IsFailure)
        {
            return current;
        }

        if (leaseExpiresAtUtc <= nowUtc)
        {
            return Result.Failure(IngestionDomainErrors.RemoteLeaseDurationInvalid);
        }

        this.RemoteLeaseExpiresAtUtc = leaseExpiresAtUtc;
        this.Version++;
        return Result.Success();
    }

    public Result CompleteRemote(
        Guid leaseId,
        long leaseEpoch,
        Guid credentialId,
        Guid workerId,
        AdapterRunOutcome outcome,
        int observedCount,
        int acceptedCount,
        int rejectedCount,
        string? acceptedCheckpoint,
        string? errorCode,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result current = this.EnsureRemoteLease(
            leaseId, leaseEpoch, credentialId, workerId, expectedVersion, nowUtc);
        if (current.IsFailure)
        {
            return current;
        }

        return this.Complete(
            outcome,
            observedCount,
            acceptedCount,
            rejectedCount,
            acceptedCheckpoint,
            errorCode,
            expectedVersion,
            nowUtc);
    }

    public Result ExpireRemoteLease(
        string? acceptedCheckpoint,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        if (this.ExecutionKind != IngestionRunExecutionKind.RemoteLease ||
            this.State != IngestionRunState.Running || expectedVersion != this.Version ||
            !this.RemoteLeaseExpiresAtUtc.HasValue)
        {
            return Result.Failure(IngestionDomainErrors.RemoteLeaseNotActive);
        }

        if (this.RemoteLeaseExpiresAtUtc > nowUtc)
        {
            return Result.Failure(IngestionDomainErrors.RemoteLeaseAlreadyActive);
        }

        return this.Complete(
            AdapterRunOutcome.Failed,
            observedCount: 0,
            acceptedCount: 0,
            rejectedCount: 0,
            acceptedCheckpoint,
            "ingestion.remote-lease-expired",
            expectedVersion,
            nowUtc);
    }

    public Result CancelRemoteLease(
        string? acceptedCheckpoint,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        if (this.ExecutionKind != IngestionRunExecutionKind.RemoteLease ||
            this.State != IngestionRunState.Running || expectedVersion != this.Version)
        {
            return Result.Failure(IngestionDomainErrors.RemoteLeaseNotActive);
        }

        return this.Complete(
            AdapterRunOutcome.Cancelled,
            observedCount: 0,
            acceptedCount: 0,
            rejectedCount: 0,
            acceptedCheckpoint,
            "ingestion.remote-lease-revoked",
            expectedVersion,
            nowUtc);
    }

    public Result Complete(
        AdapterRunOutcome outcome,
        int observedCount,
        int acceptedCount,
        int rejectedCount,
        string? acceptedCheckpoint,
        string? errorCode,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result active = this.EnsureActive(expectedVersion);
        if (active.IsFailure)
        {
            return active;
        }

        IngestionRunState finalState = outcome switch
        {
            AdapterRunOutcome.Succeeded => IngestionRunState.Succeeded,
            AdapterRunOutcome.PartiallySucceeded => IngestionRunState.PartiallySucceeded,
            AdapterRunOutcome.Failed => IngestionRunState.Failed,
            AdapterRunOutcome.Cancelled => IngestionRunState.Cancelled,
            _ => IngestionRunState.Unknown
        };
        if (finalState == IngestionRunState.Unknown)
        {
            return Result.Failure(IngestionDomainErrors.RunOutcomeInvalid);
        }

        if (observedCount < 0 || acceptedCount < 0 || rejectedCount < 0 ||
            (long)acceptedCount + rejectedCount > observedCount)
        {
            return Result.Failure(IngestionDomainErrors.RunCountsInvalid);
        }

        Result checkpoint = ValidateCheckpoint(acceptedCheckpoint);
        if (checkpoint.IsFailure)
        {
            return checkpoint;
        }

        string? normalizedErrorCode = NormalizeErrorCode(errorCode);
        bool requiresErrorCode = finalState is IngestionRunState.PartiallySucceeded or
            IngestionRunState.Failed or IngestionRunState.Cancelled;
        if (!IsValidErrorCode(normalizedErrorCode) || requiresErrorCode != (normalizedErrorCode is not null))
        {
            return Result.Failure(IngestionDomainErrors.ErrorCodeInvalid);
        }

        this.State = finalState;
        this.ObservedCount = observedCount;
        this.AcceptedCount = acceptedCount;
        this.RejectedCount = rejectedCount;
        this.AcceptedCheckpoint = NormalizeCheckpoint(acceptedCheckpoint);
        this.ErrorCode = normalizedErrorCode;
        this.CompletedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    private Result EnsureActive(long expectedVersion)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(IngestionDomainErrors.VersionConflict);
        }

        return this.State == IngestionRunState.Running
            ? Result.Success()
            : Result.Failure(IngestionDomainErrors.RunNotActive);
    }

    private Result EnsureRemoteLease(
        Guid leaseId,
        long leaseEpoch,
        Guid credentialId,
        Guid workerId,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result active = this.EnsureActive(expectedVersion);
        if (active.IsFailure)
        {
            return active;
        }

        if (this.ExecutionKind != IngestionRunExecutionKind.RemoteLease ||
            this.RemoteLeaseId != leaseId || this.RemoteLeaseEpoch != leaseEpoch ||
            this.RemoteCredentialId != credentialId || this.RemoteWorkerId != workerId)
        {
            return Result.Failure(IngestionDomainErrors.RemoteLeaseMismatch);
        }

        return this.RemoteLeaseExpiresAtUtc <= nowUtc
            ? Result.Failure(IngestionDomainErrors.RemoteLeaseExpired)
            : Result.Success();
    }

    private static Result ValidateCheckpoint(string? checkpoint)
    {
        string? normalized = NormalizeCheckpoint(checkpoint);
        return normalized?.Length > CheckpointMaxLength
            ? Result.Failure(IngestionDomainErrors.CheckpointInvalid)
            : Result.Success();
    }

    private static string? NormalizeCheckpoint(string? checkpoint) =>
        string.IsNullOrWhiteSpace(checkpoint) ? null : checkpoint.Trim();

    private static string? NormalizeErrorCode(string? errorCode) =>
        string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim().ToLowerInvariant();

    private static bool IsValidErrorCode(string? errorCode) =>
        errorCode is null ||
        (errorCode.Length <= ErrorCodeMaxLength &&
         char.IsAsciiLetterOrDigit(errorCode[0]) &&
         errorCode.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'));
}
