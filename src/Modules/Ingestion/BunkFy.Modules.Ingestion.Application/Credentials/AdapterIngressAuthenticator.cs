namespace BunkFy.Modules.Ingestion.Application.Credentials;

using System.Security.Cryptography;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Credentials;

internal sealed class AdapterIngressAuthenticator(
    IAdapterConnectionRepository connections,
    IAdapterIngressCredentialRepository credentials,
    IAdapterIngressTokenService tokens,
    IAdapterDescriptorRegistry descriptors,
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
            string scopeId = scopeContext.ScopeId.Trim();
            AdapterIngressCredential? credential = await credentials.GetForAuthenticationAsync(
                connectionId, credentialId, cancellationToken).ConfigureAwait(false);
            AdapterConnection? connection = await connections.GetAsync(
                connectionId, cancellationToken).ConfigureAwait(false);
            if (credential is null ||
                !string.Equals(credential.ScopeId, scopeId, StringComparison.Ordinal) ||
                credential.ConnectionId != connectionId ||
                connection is null ||
                !string.Equals(connection.ScopeId, scopeId, StringComparison.Ordinal) ||
                connection?.State != AdapterConnectionState.Enabled ||
                connection?.ExecutionMode != requiredMode ||
                !credential.CanAuthenticate(clock.UtcNow) ||
                credential.SecretHashAlgorithm != AdapterIngressCredential.Sha256HashAlgorithm ||
                !descriptors.TryGet(connection.AdapterType, out AdapterDescriptor? descriptor) ||
                descriptor is null ||
                !string.Equals(credential.AdapterType, descriptor.AdapterType, StringComparison.Ordinal) ||
                credential.AdapterProtocolVersion != descriptor.ProtocolVersion ||
                credential.ConfigurationSchemaVersion != descriptor.ConfigurationSchemaVersion ||
                !tokens.Verify(credential.SecretHash, candidateHash))
            {
                return Result.Failure<AdapterIngressIdentity>(IngestionApplicationErrors.IngressCredentialUnauthorized);
            }

            await credentials.MarkAuthenticatedAsync(
                credential.Id, clock.UtcNow, cancellationToken).ConfigureAwait(false);
            return Result.Success(new AdapterIngressIdentity(
                scopeId,
                connectionId,
                credential.Id,
                connection.ExecutionMode,
                credential.AdapterType,
                credential.AdapterProtocolVersion,
                credential.ConfigurationSchemaVersion,
                credential.SourceSystem,
                credential.CreatedBy));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidateHash);
        }
    }
}
