namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.DataGovernance;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;

internal sealed class CreateAdapterConnectionCommandHandler(
    IAdapterConnectionRepository connections,
    IIngestionConnectionManagementOperationRepository operations,
    IngestionExecutionMutationCoordinator execution,
    IIngestionCountryPolicyAdmission countryPolicy,
    IAdapterDescriptorRegistry descriptors,
    IScopeContext scopeContext,
    ISystemClock clock,
    IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies = null)
    : ICommandHandler<CreateAdapterConnectionCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        CreateAdapterConnectionCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ScopeRequired);
        }

        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                IngestionApplicationErrors.ConnectionManagementOperationInvalid);
        }

        Result lifecycleAdmission =
            await IngestionTenantLifecycleAdmission.AuthorizeAsync(
                lifecyclePolicies,
                scopeContext.ScopeId,
                IngestionTenantLifecycleOperation.ConnectionProvisioning,
                cancellationToken).ConfigureAwait(false);
        if (lifecycleAdmission.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                lifecycleAdmission.Error);
        }

        CountryPolicyDecision countryPolicyDecision = await countryPolicy.EvaluateAsync(
            command.PropertyId,
            IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
            CountryPolicySurface.ApiWrite,
            IngestionCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!countryPolicyDecision.IsAllowed)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                IngestionApplicationErrors.CountryPolicyDenied(countryPolicyDecision.Reason));
        }

        if (!AdapterConnectionMappings.TryMap(command.ConflictPolicy, out IngestionConflictPolicy conflictPolicy))
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConflictPolicyInvalid);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result<AdapterConnection> created = AdapterConnection.Create(
            command.OperationId,
            scopeContext.ScopeId,
            command.PropertyId,
            command.AdapterType,
            command.ExecutionMode,
            conflictPolicy,
            command.ConfigurationReference,
            command.SecretReference,
            nowUtc);
        if (created.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(created.Error);
        }

        Result capability = AdapterCapabilityValidation.Validate(
            descriptors,
            created.Value.AdapterType,
            created.Value.ExecutionMode);
        if (capability.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(capability.Error);
        }

        string requestFingerprint =
            IngestionConnectionMutationFingerprint.ComputeCreate(created.Value);
        AdapterConnection? existing = await execution.AcquireConnectionWriteAsync(
            command.OperationId,
            cancellationToken).ConfigureAwait(false);
        IngestionConnectionManagementOperationRecord? existingOperation =
            await operations.GetAsync(
                command.OperationId,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null || existingOperation is not null)
        {
            bool exactReplay = existing is not null &&
                existing.PropertyId == command.PropertyId &&
                existingOperation?.Matches(
                    IngestionConnectionManagementMutationKind.ConnectionCreate,
                    command.PropertyId,
                    command.OperationId,
                    expectedVersion: 0,
                    requestFingerprint) == true;
            return exactReplay
                ? Result.Success(AdapterConnectionMappings.MapReceipt(existing!))
                : Result.Failure<AdapterConnectionMutationReceiptDto>(
                    IngestionApplicationErrors
                        .ConnectionManagementOperationConflict);
        }

        await connections.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        await operations.AddAsync(
            new IngestionConnectionManagementOperationRecord(
                command.OperationId,
                created.Value.ScopeId,
                created.Value.PropertyId,
                created.Value.Id,
                IngestionConnectionManagementMutationKind.ConnectionCreate,
                ExpectedVersion: 0,
                requestFingerprint,
                created.Value.Version,
                CompletedAtUtc: nowUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(AdapterConnectionMappings.MapReceipt(created.Value));
    }
}

internal sealed class UpdateAdapterConnectionCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    IIngestionConnectionManagementOperationRepository operations,
    IIngestionCountryPolicyAdmission countryPolicy,
    IAdapterDescriptorRegistry descriptors,
    IScopeContext scopeContext,
    ISystemClock clock,
    IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies = null)
    : ICommandHandler<UpdateAdapterConnectionCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        UpdateAdapterConnectionCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ScopeRequired);
        }

        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                IngestionApplicationErrors.ConnectionManagementOperationInvalid);
        }

        Result lifecycleAdmission =
            await IngestionTenantLifecycleAdmission.AuthorizeAsync(
                lifecyclePolicies,
                scopeContext.ScopeId,
                IngestionTenantLifecycleOperation.ConnectionProvisioning,
                cancellationToken).ConfigureAwait(false);
        if (lifecycleAdmission.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                lifecycleAdmission.Error);
        }

        CountryPolicyDecision countryPolicyDecision = await countryPolicy.EvaluateAsync(
            command.PropertyId,
            IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
            CountryPolicySurface.ApiWrite,
            IngestionCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!countryPolicyDecision.IsAllowed)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                IngestionApplicationErrors.CountryPolicyDenied(countryPolicyDecision.Reason));
        }

        AdapterConnection? connection = await execution.AcquireConnectionWriteAsync(
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        Result capability = AdapterCapabilityValidation.Validate(
            descriptors, connection.AdapterType, command.ExecutionMode);
        if (capability.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(capability.Error);
        }

        if (!AdapterConnectionMappings.TryMap(command.ConflictPolicy, out IngestionConflictPolicy conflictPolicy))
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConflictPolicyInvalid);
        }

        Result<ResolvedSecretReference> secretReference = ResolveSecretReferenceUpdate(connection, command);
        if (secretReference.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(secretReference.Error);
        }

        string requestFingerprint =
            IngestionConnectionMutationFingerprint.ComputeUpdate(command, conflictPolicy);
        IngestionConnectionManagementOperationRecord? existingOperation =
            await operations.GetAsync(
                command.ConnectionId,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (existingOperation is not null)
        {
            return existingOperation.Matches(
                IngestionConnectionManagementMutationKind.ConnectionUpdate,
                command.PropertyId,
                command.ConnectionId,
                command.ExpectedVersion,
                requestFingerprint)
                    ? Result.Success(AdapterConnectionMappings.MapReceipt(connection))
                    : Result.Failure<AdapterConnectionMutationReceiptDto>(
                        IngestionApplicationErrors
                            .ConnectionManagementOperationConflict);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result configured = connection.Configure(
            command.ExecutionMode,
            conflictPolicy,
            command.ConfigurationReference,
            secretReference.Value.Value,
            command.ExpectedVersion,
            nowUtc);
        if (configured.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(configured.Error);
        }

        await operations.AddAsync(
            new IngestionConnectionManagementOperationRecord(
                command.OperationId,
                connection.ScopeId,
                connection.PropertyId,
                connection.Id,
                IngestionConnectionManagementMutationKind.ConnectionUpdate,
                command.ExpectedVersion,
                requestFingerprint,
                connection.Version,
                CompletedAtUtc: nowUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(AdapterConnectionMappings.MapReceipt(connection));
    }

    private static Result<ResolvedSecretReference> ResolveSecretReferenceUpdate(
        AdapterConnection connection,
        UpdateAdapterConnectionCommand command) => command.SecretReferenceUpdateMode switch
        {
            SecretReferenceUpdateMode.Keep when command.SecretReference is null =>
                Result.Success(new ResolvedSecretReference(connection.SecretReference)),
            SecretReferenceUpdateMode.Replace when !string.IsNullOrWhiteSpace(command.SecretReference) =>
                Result.Success(new ResolvedSecretReference(command.SecretReference)),
            SecretReferenceUpdateMode.Clear when command.SecretReference is null =>
                Result.Success(new ResolvedSecretReference(null)),
            _ => Result.Failure<ResolvedSecretReference>(IngestionApplicationErrors.SecretReferenceUpdateInvalid)
        };

    private sealed record ResolvedSecretReference(string? Value);
}

internal sealed class SetAdapterConnectionEnabledCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    IIngestionCountryPolicyAdmission countryPolicy,
    ISystemClock clock)
    : ICommandHandler<SetAdapterConnectionEnabledCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        SetAdapterConnectionEnabledCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await execution.AcquireConnectionWriteAsync(
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        if (command.Enabled)
        {
            CountryPolicyDecision countryPolicyDecision = await countryPolicy.EvaluateAsync(
                command.PropertyId,
                IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
                CountryPolicySurface.ApiWrite,
                IngestionCountryPolicyAdmission.AuthorizedOperatorProvenance,
                cancellationToken).ConfigureAwait(false);
            if (!countryPolicyDecision.IsAllowed)
            {
                return Result.Failure<AdapterConnectionMutationReceiptDto>(
                    IngestionApplicationErrors.CountryPolicyDenied(countryPolicyDecision.Reason));
            }
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (!command.Enabled && connection.RemoteLeaseRunId is { } remoteRunId)
        {
            BunkFy.Modules.Ingestion.Domain.Runs.IngestionRun? run =
                await execution.AcquireRunWriteAsync(
                    remoteRunId,
                    cancellationToken).ConfigureAwait(false);
            if (run is { State: BunkFy.Modules.Ingestion.Domain.Runs.IngestionRunState.Running })
            {
                Result cancelled = run.CancelRemoteLease(connection.Checkpoint, run.Version, nowUtc);
                if (cancelled.IsFailure)
                {
                    return Result.Failure<AdapterConnectionMutationReceiptDto>(cancelled.Error);
                }
            }
        }

        Result changed = command.Enabled
            ? connection.Enable(command.ExpectedVersion, nowUtc)
            : connection.Disable(command.ExpectedVersion, nowUtc);
        return changed.IsSuccess
            ? Result.Success(AdapterConnectionMappings.MapReceipt(connection))
            : Result.Failure<AdapterConnectionMutationReceiptDto>(changed.Error);
    }
}

internal sealed class ConfigureAdapterConnectionPollingScheduleCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    IAdapterDescriptorRegistry descriptors,
    ISystemClock clock)
    : ICommandHandler<ConfigureAdapterConnectionPollingScheduleCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        ConfigureAdapterConnectionPollingScheduleCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await execution.AcquireConnectionWriteAsync(
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        Result capability = AdapterCapabilityValidation.Validate(
            descriptors, connection.AdapterType, AdapterExecutionMode.Polling);
        if (capability.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(capability.Error);
        }

        _ = descriptors.TryGet(connection.AdapterType, out AdapterDescriptor? descriptor);
        if (descriptor?.Polling is { } polling &&
            command.IntervalSeconds < polling.MinimumInterval.TotalSeconds)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                IngestionApplicationErrors.PollingIntervalBelowAdapterMinimum);
        }

        Result configured = connection.ConfigurePollingSchedule(
            command.IntervalSeconds, command.MaxAttempts, command.ExpectedVersion, clock.UtcNow);
        return configured.IsSuccess
            ? Result.Success(AdapterConnectionMappings.MapReceipt(connection))
            : Result.Failure<AdapterConnectionMutationReceiptDto>(configured.Error);
    }
}

internal sealed class ClearAdapterConnectionPollingScheduleCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    ISystemClock clock)
    : ICommandHandler<ClearAdapterConnectionPollingScheduleCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        ClearAdapterConnectionPollingScheduleCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await execution.AcquireConnectionWriteAsync(
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        Result cleared = connection.ClearPollingSchedule(command.ExpectedVersion, clock.UtcNow);
        return cleared.IsSuccess
            ? Result.Success(AdapterConnectionMappings.MapReceipt(connection))
            : Result.Failure<AdapterConnectionMutationReceiptDto>(cleared.Error);
    }
}

internal sealed class ResetAdapterConnectionCheckpointCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    ISystemClock clock)
    : ICommandHandler<ResetAdapterConnectionCheckpointCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        ResetAdapterConnectionCheckpointCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await execution.AcquireConnectionWriteAsync(
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        Result reset = connection.ResetCheckpoint(command.ExpectedVersion, clock.UtcNow);
        return reset.IsSuccess
            ? Result.Success(AdapterConnectionMappings.MapReceipt(connection))
            : Result.Failure<AdapterConnectionMutationReceiptDto>(reset.Error);
    }
}
