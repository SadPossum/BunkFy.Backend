namespace BunkFy.Modules.Ingestion.Domain.Connections;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Domain.Errors;

public sealed partial class AdapterConnection
{
    public static Result<AdapterConnection> Create(
        Guid connectionId,
        string scopeId,
        Guid propertyId,
        string adapterType,
        AdapterExecutionMode executionMode,
        IngestionConflictPolicy conflictPolicy,
        string configurationReference,
        string? secretReference,
        DateTimeOffset nowUtc)
    {
        if (connectionId == Guid.Empty)
        {
            return Result.Failure<AdapterConnection>(IngestionDomainErrors.ConnectionIdRequired);
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<AdapterConnection>(IngestionDomainErrors.ScopeRequired);
        }

        if (propertyId == Guid.Empty)
        {
            return Result.Failure<AdapterConnection>(IngestionDomainErrors.PropertyIdRequired);
        }

        string normalizedAdapterType = adapterType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedAdapterType.Length is 0 or > AdapterTypeMaxLength)
        {
            return Result.Failure<AdapterConnection>(IngestionDomainErrors.AdapterTypeInvalid);
        }

        if (executionMode is not (AdapterExecutionMode.Polling or AdapterExecutionMode.Continuous or
            AdapterExecutionMode.Push or AdapterExecutionMode.RemotePolling))
        {
            return Result.Failure<AdapterConnection>(IngestionDomainErrors.ExecutionModeInvalid);
        }

        if (conflictPolicy is not (IngestionConflictPolicy.SuggestionsOnly or IngestionConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged))
        {
            return Result.Failure<AdapterConnection>(IngestionDomainErrors.ConflictPolicyInvalid);
        }

        string normalizedConfigurationReference = configurationReference?.Trim() ?? string.Empty;
        if (normalizedConfigurationReference.Length is 0 or > ReferenceMaxLength)
        {
            return Result.Failure<AdapterConnection>(IngestionDomainErrors.ConfigurationReferenceInvalid);
        }

        string? normalizedSecretReference = NormalizeOptional(secretReference);
        if (normalizedSecretReference?.Length > ReferenceMaxLength)
        {
            return Result.Failure<AdapterConnection>(IngestionDomainErrors.SecretReferenceInvalid);
        }

        return Result.Success(new AdapterConnection(connectionId, scopeId.Trim())
        {
            PropertyId = propertyId,
            AdapterType = normalizedAdapterType,
            ExecutionMode = executionMode,
            ConflictPolicy = conflictPolicy,
            ConfigurationReference = normalizedConfigurationReference,
            SecretReference = normalizedSecretReference,
            CreatedAtUtc = nowUtc
        });
    }

    public Result Disable(long expectedVersion, DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        if (this.State == AdapterConnectionState.Disabled)
        {
            return Result.Failure(IngestionDomainErrors.ConnectionAlreadyDisabled);
        }

        this.State = AdapterConnectionState.Disabled;
        this.ClearRemoteLease();
        this.Touch(nowUtc);
        return Result.Success();
    }

    public Result ConfigurePollingSchedule(
        int intervalSeconds,
        int maxAttempts,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        if (this.ExecutionMode != AdapterExecutionMode.Polling)
        {
            return Result.Failure(IngestionDomainErrors.PollingScheduleRequiresPollingMode);
        }

        if (intervalSeconds is < MinimumPollingIntervalSeconds or > MaximumPollingIntervalSeconds)
        {
            return Result.Failure(IngestionDomainErrors.PollingIntervalInvalid);
        }

        if (maxAttempts is <= 0 or > MaximumPollingScheduleAttempts)
        {
            return Result.Failure(IngestionDomainErrors.PollingScheduleAttemptsInvalid);
        }

        if (this.PollingIntervalSeconds == intervalSeconds && this.PollingScheduleMaxAttempts == maxAttempts)
        {
            return Result.Success();
        }

        this.PollingIntervalSeconds = intervalSeconds;
        this.PollingScheduleMaxAttempts = maxAttempts;
        this.PollingScheduleConfiguredAtUtc = nowUtc;
        this.Touch(nowUtc);
        return Result.Success();
    }

    public Result ClearPollingSchedule(long expectedVersion, DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        if (!this.PollingIntervalSeconds.HasValue)
        {
            return Result.Success();
        }

        this.PollingIntervalSeconds = null;
        this.PollingScheduleMaxAttempts = null;
        this.PollingScheduleConfiguredAtUtc = null;
        this.Touch(nowUtc);
        return Result.Success();
    }

    public Result Enable(long expectedVersion, DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        if (this.State == AdapterConnectionState.Enabled)
        {
            return Result.Failure(IngestionDomainErrors.ConnectionAlreadyEnabled);
        }

        this.State = AdapterConnectionState.Enabled;
        this.Touch(nowUtc);
        return Result.Success();
    }

    public Result Configure(
        AdapterExecutionMode executionMode,
        IngestionConflictPolicy conflictPolicy,
        string configurationReference,
        string? secretReference,
        long expectedVersion,
        DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        if (executionMode is not (AdapterExecutionMode.Polling or AdapterExecutionMode.Continuous or
            AdapterExecutionMode.Push or AdapterExecutionMode.RemotePolling))
        {
            return Result.Failure(IngestionDomainErrors.ExecutionModeInvalid);
        }

        if (conflictPolicy is not (IngestionConflictPolicy.SuggestionsOnly or IngestionConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged))
        {
            return Result.Failure(IngestionDomainErrors.ConflictPolicyInvalid);
        }

        string normalizedConfigurationReference = configurationReference?.Trim() ?? string.Empty;
        string? normalizedSecretReference = NormalizeOptional(secretReference);
        if (normalizedConfigurationReference.Length is 0 or > ReferenceMaxLength)
        {
            return Result.Failure(IngestionDomainErrors.ConfigurationReferenceInvalid);
        }

        if (normalizedSecretReference?.Length > ReferenceMaxLength)
        {
            return Result.Failure(IngestionDomainErrors.SecretReferenceInvalid);
        }

        if (this.PollingIntervalSeconds.HasValue && executionMode != AdapterExecutionMode.Polling)
        {
            return Result.Failure(IngestionDomainErrors.PollingScheduleRequiresPollingMode);
        }

        if (this.ExecutionMode == AdapterExecutionMode.RemotePolling &&
            executionMode != AdapterExecutionMode.RemotePolling &&
            this.RemoteLeaseId.HasValue)
        {
            return Result.Failure(IngestionDomainErrors.RemoteLeaseMustBeReleased);
        }

        if (this.ExecutionMode == executionMode && this.ConflictPolicy == conflictPolicy &&
            this.ConfigurationReference == normalizedConfigurationReference &&
            this.SecretReference == normalizedSecretReference)
        {
            return Result.Success();
        }

        this.ExecutionMode = executionMode;
        this.ConflictPolicy = conflictPolicy;
        this.ConfigurationReference = normalizedConfigurationReference;
        this.SecretReference = normalizedSecretReference;
        this.Touch(nowUtc);
        return Result.Success();
    }

    public Result ResetCheckpoint(long expectedVersion, DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        if (this.State != AdapterConnectionState.Disabled)
        {
            return Result.Failure(IngestionDomainErrors.ConnectionMustBeDisabled);
        }

        if (this.Checkpoint is null)
        {
            return Result.Success();
        }

        this.Checkpoint = null;
        this.Touch(nowUtc);
        return Result.Success();
    }

    public Result AdvanceCheckpoint(string? checkpoint, long expectedVersion, DateTimeOffset nowUtc)
    {
        Result version = this.EnsureVersion(expectedVersion);
        if (version.IsFailure)
        {
            return version;
        }

        if (this.State != AdapterConnectionState.Enabled)
        {
            return Result.Failure(IngestionDomainErrors.ConnectionNotEnabled);
        }

        string? normalizedCheckpoint = NormalizeOptional(checkpoint);
        if (normalizedCheckpoint?.Length > CheckpointMaxLength)
        {
            return Result.Failure(IngestionDomainErrors.CheckpointInvalid);
        }

        this.Checkpoint = normalizedCheckpoint;
        this.Touch(nowUtc);
        return Result.Success();
    }

}
