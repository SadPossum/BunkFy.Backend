namespace BunkFy.Modules.Ingestion.Application.Credentials;

using System.Security.Cryptography;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Credentials;

internal sealed class AdapterIngressAuthenticator(
    IAdapterConnectionRepository connections,
    IAdapterIngressCredentialRepository credentials,
    IAdapterIngressTokenService tokens,
    IScopeContext scopeContext,
    ISystemClock clock)
    : IAdapterIngressAuthenticator
{
    public async Task<Result<AdapterIngressIdentity>> AuthenticateAsync(
        Guid connectionId,
        string token,
        AdapterExecutionMode requiredMode,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId) ||
            connectionId == Guid.Empty ||
            !tokens.TryResolve(token, out Guid credentialId, out byte[] candidateHash))
        {
            return Result.Failure<AdapterIngressIdentity>(IngestionApplicationErrors.IngressCredentialUnauthorized);
        }

        try
        {
            AdapterIngressCredential? credential = await credentials.GetForAuthenticationAsync(
                connectionId, credentialId, cancellationToken).ConfigureAwait(false);
            BunkFy.Modules.Ingestion.Domain.Connections.AdapterConnection? connection = await connections.GetAsync(
                connectionId, cancellationToken).ConfigureAwait(false);
            if (credential is null ||
                connection?.ExecutionMode != requiredMode ||
                !credential.CanAuthenticate(clock.UtcNow) ||
                credential.SecretHashAlgorithm != AdapterIngressCredential.Sha256HashAlgorithm ||
                !tokens.Verify(credential.SecretHash, candidateHash))
            {
                return Result.Failure<AdapterIngressIdentity>(IngestionApplicationErrors.IngressCredentialUnauthorized);
            }

            await credentials.MarkAuthenticatedAsync(
                credential.Id, clock.UtcNow, cancellationToken).ConfigureAwait(false);
            return Result.Success(new AdapterIngressIdentity(
                scopeContext.ScopeId.Trim(), connectionId, credential.Id, connection.ExecutionMode));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidateHash);
        }
    }
}
