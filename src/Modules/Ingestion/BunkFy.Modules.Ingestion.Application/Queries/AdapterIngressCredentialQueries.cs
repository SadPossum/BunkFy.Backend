namespace BunkFy.Modules.Ingestion.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Contracts;

public sealed record ListAdapterIngressCredentialsQuery(
    Guid PropertyId,
    Guid ConnectionId,
    int Page,
    int PageSize)
    : IQuery<AdapterIngressCredentialListResponse>;
