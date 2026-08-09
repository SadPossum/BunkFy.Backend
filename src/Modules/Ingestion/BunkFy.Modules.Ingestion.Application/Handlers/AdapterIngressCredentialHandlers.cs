namespace BunkFy.Modules.Ingestion.Application.Handlers;

using System.Security.Cryptography;
using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class CreateAdapterIngressCredentialCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    IAdapterIngressCredentialRepository credentials,
    IIngestionConnectionManagementOperationRepository operations,
    IAdapterIngressTokenService tokens,
    IAdapterDescriptorRegistry descriptors,
    IScopeContext scopeContext,
    ISystemClock clock,
    IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies = null)
    : ICommandHandler<
        CreateAdapterIngressCredentialCommand,
        CreateAdapterIngressCredentialResponse>
{
    public async Task<Result<CreateAdapterIngressCredentialResponse>> HandleAsync(
        CreateAdapterIngressCredentialCommand command,
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
            return Failure(admission.Error);
        }

        if (command.OperationId == command.ConnectionId)
        {
            return Failure(
                IngestionApplicationErrors
                    .ConnectionManagementOperationInvalid);
        }

        AdapterConnection? connection =
            await execution.AcquireConnectionWriteAsync(
                command.ConnectionId,
                cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Failure(IngestionApplicationErrors.ConnectionNotFound);
        }

        if (connection.ExecutionMode is not (
            AdapterExecutionMode.Push or
            AdapterExecutionMode.RemotePolling))
        {
            return Failure(
                IngestionApplicationErrors
                    .IngressCredentialsRequirePushMode);
        }

        if (!descriptors.TryGet(
                connection.AdapterType,
                out AdapterDescriptor? descriptor) ||
            descriptor is null)
        {
            return Failure(
                IngestionApplicationErrors.AdapterTypeNotRegistered);
        }

        if (!descriptor.ExecutionModes.Contains(connection.ExecutionMode))
        {
            return Failure(
                IngestionApplicationErrors.AdapterExecutionModeUnsupported);
        }

        string sourceSystem =
            command.SourceSystem ?? descriptor.AdapterType;
        string requestFingerprint =
            IngestionCredentialMutationFingerprint.ComputeCreate(
                command,
                descriptor,
                sourceSystem);
        AdapterIngressCredential? existing =
            await credentials.GetAsync(
                connection.Id,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        IngestionConnectionManagementOperationRecord? existingOperation =
            await operations.GetAsync(
                connection.Id,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null || existingOperation is not null)
        {
            bool exactReplay =
                existing is not null &&
                existingOperation?.Matches(
                    IngestionConnectionManagementMutationKind
                        .AdapterIngressCredentialCreate,
                    command.PropertyId,
                    command.ConnectionId,
                    expectedVersion: 0,
                    requestFingerprint) == true;
            return exactReplay
                ? Result.Success(
                    new CreateAdapterIngressCredentialResponse(
                        AdapterIngressCredentialMappings.Map(existing!),
                        AdapterIngressCredentialIssuanceOutcome.AlreadyIssued,
                        Token: null))
                : Failure(
                    IngestionApplicationErrors
                        .ConnectionManagementOperationConflict);
        }

        if (await credentials.IdExistsAsync(
                command.OperationId,
                cancellationToken).ConfigureAwait(false))
        {
            return Failure(
                IngestionApplicationErrors
                    .ConnectionManagementOperationConflict);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        int? slot = await credentials.GetAvailableSlotAsync(
            connection.Id,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        if (!slot.HasValue)
        {
            return Failure(
                IngestionApplicationErrors.IngressCredentialLimitReached);
        }

        AdapterIngressTokenIssue token = tokens.Issue(command.OperationId);
        try
        {
            DateTimeOffset expiresAtUtc =
                command.ExpiresAtUtc ??
                nowUtc.Add(AdapterIngressCredential.DefaultLifetime);
            Result<AdapterIngressCredential> created =
                AdapterIngressCredential.Create(
                    command.OperationId,
                    admission.Value,
                    connection.Id,
                    descriptor.AdapterType,
                    descriptor.ProtocolVersion,
                    descriptor.ConfigurationSchemaVersion,
                    sourceSystem,
                    slot.Value,
                    command.Label,
                    token.HashAlgorithm,
                    token.SecretHash,
                    expiresAtUtc,
                    command.CreatedBy,
                    nowUtc);
            if (created.IsFailure)
            {
                return Failure(created.Error);
            }

            await credentials.AddAsync(
                created.Value,
                cancellationToken).ConfigureAwait(false);
            await operations.AddAsync(
                new IngestionConnectionManagementOperationRecord(
                    command.OperationId,
                    created.Value.ScopeId,
                    command.PropertyId,
                    connection.Id,
                    IngestionConnectionManagementMutationKind
                        .AdapterIngressCredentialCreate,
                    ExpectedVersion: 0,
                    requestFingerprint,
                    created.Value.Version,
                    CompletedAtUtc: nowUtc),
                cancellationToken).ConfigureAwait(false);
            return Result.Success(
                new CreateAdapterIngressCredentialResponse(
                    AdapterIngressCredentialMappings.Map(created.Value),
                    AdapterIngressCredentialIssuanceOutcome.Issued,
                    token.Token));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(token.SecretHash);
        }
    }

    private static Result<CreateAdapterIngressCredentialResponse> Failure(
        Error error) =>
        Result.Failure<CreateAdapterIngressCredentialResponse>(error);
}

internal sealed class RevokeAdapterIngressCredentialCommandHandler(
    IngestionExecutionMutationCoordinator execution,
    IAdapterIngressCredentialRepository credentials,
    IIngestionConnectionManagementOperationRepository operations,
    IScopeContext scopeContext,
    ISystemClock clock,
    IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies = null)
    : ICommandHandler<
        RevokeAdapterIngressCredentialCommand,
        AdapterIngressCredentialMutationReceiptDto>
{
    public async Task<Result<AdapterIngressCredentialMutationReceiptDto>> HandleAsync(
        RevokeAdapterIngressCredentialCommand command,
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
            return Result
                .Failure<AdapterIngressCredentialMutationReceiptDto>(
                    admission.Error);
        }

        AdapterConnection? connection =
            await execution.AcquireConnectionWriteAsync(
                command.ConnectionId,
                cancellationToken).ConfigureAwait(false);
        if (connection is null || connection.PropertyId != command.PropertyId)
        {
            return Result
                .Failure<AdapterIngressCredentialMutationReceiptDto>(
                    IngestionApplicationErrors.ConnectionNotFound);
        }

        AdapterIngressCredential? credential = await credentials.GetAsync(
            connection.Id,
            command.CredentialId,
            cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            return Result
                .Failure<AdapterIngressCredentialMutationReceiptDto>(
                    IngestionApplicationErrors.IngressCredentialNotFound);
        }

        string requestFingerprint =
            IngestionCredentialMutationFingerprint.ComputeRevoke(command);
        IngestionConnectionManagementOperationRecord? existingOperation =
            await operations.GetAsync(
                connection.Id,
                command.OperationId,
                cancellationToken).ConfigureAwait(false);
        if (existingOperation is not null)
        {
            return existingOperation.Matches(
                IngestionConnectionManagementMutationKind
                    .AdapterIngressCredentialRevoke,
                command.PropertyId,
                command.ConnectionId,
                command.ExpectedVersion,
                requestFingerprint)
                    ? Result.Success(
                        AdapterIngressCredentialMappings.MapReceipt(
                            credential))
                    : Result
                        .Failure<AdapterIngressCredentialMutationReceiptDto>(
                            IngestionApplicationErrors
                                .ConnectionManagementOperationConflict);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result revoked = credential.Revoke(
            command.ExpectedVersion,
            command.RevokedBy,
            nowUtc);
        if (revoked.IsFailure)
        {
            return Result
                .Failure<AdapterIngressCredentialMutationReceiptDto>(
                    revoked.Error);
        }

        await operations.AddAsync(
            new IngestionConnectionManagementOperationRecord(
                command.OperationId,
                credential.ScopeId,
                command.PropertyId,
                connection.Id,
                IngestionConnectionManagementMutationKind
                    .AdapterIngressCredentialRevoke,
                command.ExpectedVersion,
                requestFingerprint,
                credential.Version,
                CompletedAtUtc: nowUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(
            AdapterIngressCredentialMappings.MapReceipt(credential));
    }
}

internal sealed class ListAdapterIngressCredentialsQueryHandler(
    IAdapterConnectionRepository connections,
    IAdapterIngressCredentialReader credentials)
    : IQueryHandler<
        ListAdapterIngressCredentialsQuery,
        AdapterIngressCredentialListResponse>
{
    public async Task<Result<AdapterIngressCredentialListResponse>> HandleAsync(
        ListAdapterIngressCredentialsQuery query,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await connections.GetAsync(
            query.PropertyId,
            query.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<AdapterIngressCredentialListResponse>(
                IngestionApplicationErrors.ConnectionNotFound);
        }

        return Result.Success(await credentials.ListAsync(
            connection.Id,
            PageRequest.Normalize(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
    }
}
