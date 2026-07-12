namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Results;

public interface IAdapterIngressAuthenticator
{
    Task<Result<AdapterIngressIdentity>> AuthenticateAsync(
        Guid connectionId,
        string token,
        AdapterExecutionMode requiredMode,
        CancellationToken cancellationToken);
}

public sealed record AdapterIngressIdentity(
    string ScopeId,
    Guid ConnectionId,
    Guid CredentialId,
    AdapterExecutionMode ExecutionMode);
