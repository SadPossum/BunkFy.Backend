namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;

public sealed record ClaimRemoteAdapterLeaseCommand(
    Guid ConnectionId,
    Guid CredentialId,
    AdapterRemoteLeaseClaimRequest Request)
    : ITransactionalCommand<AdapterRemoteLeaseClaimResponse>;

public sealed record RenewRemoteAdapterLeaseCommand(
    Guid ConnectionId,
    Guid CredentialId,
    AdapterRemoteLeaseRenewRequest Request)
    : ITransactionalCommand<AdapterRemoteLeaseRenewResponse>;

public sealed record CompleteRemoteAdapterRunCommand(
    Guid ConnectionId,
    Guid CredentialId,
    AdapterRemoteRunCompletionRequest Request)
    : ITransactionalCommand<AdapterRemoteRunCompletionResponse>;
