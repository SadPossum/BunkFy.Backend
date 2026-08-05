namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.DataGovernance;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
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
    IIngestionCountryPolicyAdmission countryPolicy,
    IAdapterDescriptorRegistry descriptors,
    IScopeContext scopeContext,
    IIdGenerator idGenerator,
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

        Result capability = AdapterCapabilityValidation.Validate(
            descriptors, command.AdapterType, command.ExecutionMode);
        if (capability.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(capability.Error);
        }

        if (!AdapterConnectionMappings.TryMap(command.ConflictPolicy, out IngestionConflictPolicy conflictPolicy))
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConflictPolicyInvalid);
        }

        Result<AdapterConnection> created = AdapterConnection.Create(
            idGenerator.NewId(),
            scopeContext.ScopeId,
            command.PropertyId,
            command.AdapterType,
            command.ExecutionMode,
            conflictPolicy,
            command.ConfigurationReference,
            command.SecretReference,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(created.Error);
        }

        await connections.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(AdapterConnectionMappings.MapReceipt(created.Value));
    }
}

internal sealed class UpdateAdapterConnectionCommandHandler(
    IAdapterConnectionRepository connections,
    IAdapterDescriptorRegistry descriptors,
    ISystemClock clock)
    : ICommandHandler<UpdateAdapterConnectionCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        UpdateAdapterConnectionCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await connections.GetAsync(
            command.PropertyId,
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null)
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

        Result configured = connection.Configure(
            command.ExecutionMode,
            conflictPolicy,
            command.ConfigurationReference,
            secretReference.Value.Value,
            command.ExpectedVersion,
            clock.UtcNow);
        return configured.IsSuccess
            ? Result.Success(AdapterConnectionMappings.MapReceipt(connection))
            : Result.Failure<AdapterConnectionMutationReceiptDto>(configured.Error);
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
    IAdapterConnectionRepository connections,
    IIngestionRunRepository runs,
    IIngestionCountryPolicyAdmission countryPolicy,
    ISystemClock clock)
    : ICommandHandler<SetAdapterConnectionEnabledCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        SetAdapterConnectionEnabledCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await connections.GetAsync(
            command.PropertyId,
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null)
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
            BunkFy.Modules.Ingestion.Domain.Runs.IngestionRun? run = await runs.GetAsync(
                remoteRunId, cancellationToken).ConfigureAwait(false);
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
    IAdapterConnectionRepository connections,
    IAdapterDescriptorRegistry descriptors,
    ISystemClock clock)
    : ICommandHandler<ConfigureAdapterConnectionPollingScheduleCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        ConfigureAdapterConnectionPollingScheduleCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await connections.GetAsync(
            command.PropertyId, command.ConnectionId, cancellationToken).ConfigureAwait(false);
        if (connection is null)
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
    IAdapterConnectionRepository connections,
    ISystemClock clock)
    : ICommandHandler<ClearAdapterConnectionPollingScheduleCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        ClearAdapterConnectionPollingScheduleCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await connections.GetAsync(
            command.PropertyId, command.ConnectionId, cancellationToken).ConfigureAwait(false);
        if (connection is null)
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
    IAdapterConnectionRepository connections,
    ISystemClock clock)
    : ICommandHandler<ResetAdapterConnectionCheckpointCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        ResetAdapterConnectionCheckpointCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await connections.GetAsync(
            command.PropertyId,
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        Result reset = connection.ResetCheckpoint(command.ExpectedVersion, clock.UtcNow);
        return reset.IsSuccess
            ? Result.Success(AdapterConnectionMappings.MapReceipt(connection))
            : Result.Failure<AdapterConnectionMutationReceiptDto>(reset.Error);
    }
}
