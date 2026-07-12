namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Credentials;

internal sealed class CreateAdapterIngressCredentialCommandHandler(
    IAdapterConnectionRepository connections,
    IAdapterIngressCredentialRepository credentials,
    IAdapterIngressTokenService tokens,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<CreateAdapterIngressCredentialCommand, CreateAdapterIngressCredentialResponse>
{
    public async Task<Result<CreateAdapterIngressCredentialResponse>> HandleAsync(
        CreateAdapterIngressCredentialCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<CreateAdapterIngressCredentialResponse>(IngestionApplicationErrors.ScopeRequired);
        }

        AdapterConnection? connection = await connections.GetAsync(
            command.PropertyId, command.ConnectionId, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<CreateAdapterIngressCredentialResponse>(IngestionApplicationErrors.ConnectionNotFound);
        }

        if (connection.ExecutionMode is not (
            BunkFy.Adapter.Abstractions.AdapterExecutionMode.Push or
            BunkFy.Adapter.Abstractions.AdapterExecutionMode.RemotePolling))
        {
            return Result.Failure<CreateAdapterIngressCredentialResponse>(
                IngestionApplicationErrors.IngressCredentialsRequirePushMode);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        int? slot = await credentials.GetAvailableSlotAsync(
            connection.Id, nowUtc, cancellationToken).ConfigureAwait(false);
        if (!slot.HasValue)
        {
            return Result.Failure<CreateAdapterIngressCredentialResponse>(
                IngestionApplicationErrors.IngressCredentialLimitReached);
        }

        Guid credentialId = ids.NewId();
        AdapterIngressTokenIssue token = tokens.Issue(credentialId);
        DateTimeOffset expiresAtUtc = command.ExpiresAtUtc ?? nowUtc.Add(AdapterIngressCredential.DefaultLifetime);
        Result<AdapterIngressCredential> created = AdapterIngressCredential.Create(
            credentialId,
            scopeContext.ScopeId,
            connection.Id,
            slot.Value,
            command.Label,
            token.HashAlgorithm,
            token.SecretHash,
            expiresAtUtc,
            command.CreatedBy,
            nowUtc);
        if (created.IsFailure)
        {
            return Result.Failure<CreateAdapterIngressCredentialResponse>(created.Error);
        }

        await credentials.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new CreateAdapterIngressCredentialResponse(
            AdapterIngressCredentialMappings.Map(created.Value),
            token.Token));
    }
}

internal sealed class RevokeAdapterIngressCredentialCommandHandler(
    IAdapterConnectionRepository connections,
    IAdapterIngressCredentialRepository credentials,
    ISystemClock clock)
    : ICommandHandler<RevokeAdapterIngressCredentialCommand, AdapterIngressCredentialDto>
{
    public async Task<Result<AdapterIngressCredentialDto>> HandleAsync(
        RevokeAdapterIngressCredentialCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await connections.GetAsync(
            command.PropertyId, command.ConnectionId, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<AdapterIngressCredentialDto>(IngestionApplicationErrors.ConnectionNotFound);
        }

        AdapterIngressCredential? credential = await credentials.GetAsync(
            connection.Id, command.CredentialId, cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            return Result.Failure<AdapterIngressCredentialDto>(IngestionApplicationErrors.IngressCredentialNotFound);
        }

        Result revoked = credential.Revoke(command.ExpectedVersion, command.RevokedBy, clock.UtcNow);
        return revoked.IsSuccess
            ? Result.Success(AdapterIngressCredentialMappings.Map(credential))
            : Result.Failure<AdapterIngressCredentialDto>(revoked.Error);
    }
}

internal sealed class ListAdapterIngressCredentialsQueryHandler(
    IAdapterConnectionRepository connections,
    IAdapterIngressCredentialReader credentials)
    : IQueryHandler<ListAdapterIngressCredentialsQuery, AdapterIngressCredentialListResponse>
{
    public async Task<Result<AdapterIngressCredentialListResponse>> HandleAsync(
        ListAdapterIngressCredentialsQuery query,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await connections.GetAsync(
            query.PropertyId, query.ConnectionId, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<AdapterIngressCredentialListResponse>(IngestionApplicationErrors.ConnectionNotFound);
        }

        return Result.Success(await credentials.ListAsync(
            connection.Id,
            PageRequest.Normalize(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
    }
}
