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
        Result<string> admission =
            await IngestionManagementAdmission.AuthorizeAsync(
                scopeContext,
                lifecyclePolicies,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                admission.Error);
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
            admission.Value,
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
        Result<string> admission =
            await IngestionManagementAdmission.AuthorizeAsync(
                scopeContext,
                lifecyclePolicies,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result.Failure<AdapterConnectionMutationReceiptDto>(
                admission.Error);
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
