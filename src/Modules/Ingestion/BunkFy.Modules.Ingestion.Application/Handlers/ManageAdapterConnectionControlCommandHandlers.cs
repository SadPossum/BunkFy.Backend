namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Adapter.Abstractions;
using BunkFy.DataGovernance;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class SetAdapterConnectionEnabledCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    IIngestionConnectionManagementOperationRepository operations,
    IIngestionCountryPolicyAdmission countryPolicy,
    IScopeContext scopeContext,
    ISystemClock clock,
    IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies = null)
    : ICommandHandler<SetAdapterConnectionEnabledCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        SetAdapterConnectionEnabledCommand command,
        CancellationToken cancellationToken)
    {
        Result<string> admission =
            await IngestionConnectionManagementAdmission.AuthorizeAsync(
                scopeContext,
                lifecyclePolicies,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                admission.Error);
        }

        if (command.Enabled)
        {
            CountryPolicyDecision countryPolicyDecision =
                await countryPolicy.EvaluateAsync(
                    command.PropertyId,
                    IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
                    CountryPolicySurface.ApiWrite,
                    IngestionCountryPolicyAdmission.AuthorizedOperatorProvenance,
                    cancellationToken).ConfigureAwait(false);
            if (!countryPolicyDecision.IsAllowed)
            {
                return Result.Failure<AdapterConnectionMutationReceiptDto>(
                    IngestionApplicationErrors.CountryPolicyDenied(
                        countryPolicyDecision.Reason));
            }
        }

        AdapterConnection? connection = await execution.AcquireConnectionWriteAsync(
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        IngestionConnectionManagementMutationKind kind = command.Enabled
            ? IngestionConnectionManagementMutationKind.ConnectionEnable
            : IngestionConnectionManagementMutationKind.ConnectionDisable;
        string requestFingerprint =
            IngestionConnectionMutationFingerprint.ComputeEnabledState(command);
        IngestionConnectionManagementOperationRecord? existingOperation =
            await operations.GetAsync(
                command.ConnectionId,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (existingOperation is not null)
        {
            return existingOperation.Matches(
                kind,
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
        if (changed.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                changed.Error);
        }

        await operations.AddAsync(
            new IngestionConnectionManagementOperationRecord(
                command.OperationId,
                connection.ScopeId,
                connection.PropertyId,
                connection.Id,
                kind,
                command.ExpectedVersion,
                requestFingerprint,
                connection.Version,
                CompletedAtUtc: nowUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(AdapterConnectionMappings.MapReceipt(connection));
    }
}

internal sealed class ConfigureAdapterConnectionPollingScheduleCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    IIngestionConnectionManagementOperationRepository operations,
    IAdapterDescriptorRegistry descriptors,
    IScopeContext scopeContext,
    ISystemClock clock,
    IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies = null)
    : ICommandHandler<ConfigureAdapterConnectionPollingScheduleCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        ConfigureAdapterConnectionPollingScheduleCommand command,
        CancellationToken cancellationToken)
    {
        Result<string> admission =
            await IngestionConnectionManagementAdmission.AuthorizeAsync(
                scopeContext,
                lifecyclePolicies,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                admission.Error);
        }

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

        string requestFingerprint =
            IngestionConnectionMutationFingerprint.ComputePollingSchedule(command);
        IngestionConnectionManagementOperationRecord? existingOperation =
            await operations.GetAsync(
                command.ConnectionId,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (existingOperation is not null)
        {
            return existingOperation.Matches(
                IngestionConnectionManagementMutationKind.PollingScheduleConfigure,
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
        Result configured = connection.ConfigurePollingSchedule(
            command.IntervalSeconds,
            command.MaxAttempts,
            command.ExpectedVersion,
            nowUtc);
        if (configured.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                configured.Error);
        }

        await operations.AddAsync(
            new IngestionConnectionManagementOperationRecord(
                command.OperationId,
                connection.ScopeId,
                connection.PropertyId,
                connection.Id,
                IngestionConnectionManagementMutationKind
                    .PollingScheduleConfigure,
                command.ExpectedVersion,
                requestFingerprint,
                connection.Version,
                CompletedAtUtc: nowUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(AdapterConnectionMappings.MapReceipt(connection));
    }
}

internal sealed class ClearAdapterConnectionPollingScheduleCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    IIngestionConnectionManagementOperationRepository operations,
    IScopeContext scopeContext,
    ISystemClock clock,
    IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies = null)
    : ICommandHandler<ClearAdapterConnectionPollingScheduleCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        ClearAdapterConnectionPollingScheduleCommand command,
        CancellationToken cancellationToken)
    {
        Result<string> admission =
            await IngestionConnectionManagementAdmission.AuthorizeAsync(
                scopeContext,
                lifecyclePolicies,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                admission.Error);
        }

        AdapterConnection? connection = await execution.AcquireConnectionWriteAsync(
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        string requestFingerprint = IngestionConnectionMutationFingerprint
            .ComputePollingScheduleClear(command);
        IngestionConnectionManagementOperationRecord? existingOperation =
            await operations.GetAsync(
                command.ConnectionId,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (existingOperation is not null)
        {
            return existingOperation.Matches(
                IngestionConnectionManagementMutationKind.PollingScheduleClear,
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
        Result cleared = connection.ClearPollingSchedule(
            command.ExpectedVersion,
            nowUtc);
        if (cleared.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                cleared.Error);
        }

        await operations.AddAsync(
            new IngestionConnectionManagementOperationRecord(
                command.OperationId,
                connection.ScopeId,
                connection.PropertyId,
                connection.Id,
                IngestionConnectionManagementMutationKind.PollingScheduleClear,
                command.ExpectedVersion,
                requestFingerprint,
                connection.Version,
                CompletedAtUtc: nowUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(AdapterConnectionMappings.MapReceipt(connection));
    }
}

internal sealed class ResetAdapterConnectionCheckpointCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    IIngestionConnectionManagementOperationRepository operations,
    IScopeContext scopeContext,
    ISystemClock clock,
    IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies = null)
    : ICommandHandler<ResetAdapterConnectionCheckpointCommand, AdapterConnectionMutationReceiptDto>
{
    public async Task<Result<AdapterConnectionMutationReceiptDto>> HandleAsync(
        ResetAdapterConnectionCheckpointCommand command,
        CancellationToken cancellationToken)
    {
        Result<string> admission =
            await IngestionConnectionManagementAdmission.AuthorizeAsync(
                scopeContext,
                lifecyclePolicies,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                admission.Error);
        }

        AdapterConnection? connection = await execution.AcquireConnectionWriteAsync(
            command.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        string requestFingerprint = IngestionConnectionMutationFingerprint
            .ComputeCheckpointReset(command);
        IngestionConnectionManagementOperationRecord? existingOperation =
            await operations.GetAsync(
                command.ConnectionId,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (existingOperation is not null)
        {
            return existingOperation.Matches(
                IngestionConnectionManagementMutationKind.CheckpointReset,
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
        Result reset = connection.ResetCheckpoint(
            command.ExpectedVersion,
            nowUtc);
        if (reset.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                reset.Error);
        }

        await operations.AddAsync(
            new IngestionConnectionManagementOperationRecord(
                command.OperationId,
                connection.ScopeId,
                connection.PropertyId,
                connection.Id,
                IngestionConnectionManagementMutationKind.CheckpointReset,
                command.ExpectedVersion,
                requestFingerprint,
                connection.Version,
                CompletedAtUtc: nowUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(AdapterConnectionMappings.MapReceipt(connection));
    }
}
