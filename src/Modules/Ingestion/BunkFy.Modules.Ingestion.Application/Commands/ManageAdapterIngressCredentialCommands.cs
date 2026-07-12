namespace BunkFy.Modules.Ingestion.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Contracts;

public sealed record CreateAdapterIngressCredentialCommand(
    Guid PropertyId,
    Guid ConnectionId,
    string Label,
    DateTimeOffset? ExpiresAtUtc,
    string CreatedBy)
    : ITransactionalCommand<CreateAdapterIngressCredentialResponse>;

public sealed record RevokeAdapterIngressCredentialCommand(
    Guid PropertyId,
    Guid ConnectionId,
    Guid CredentialId,
    long ExpectedVersion,
    string RevokedBy)
    : ITransactionalCommand<AdapterIngressCredentialDto>;
