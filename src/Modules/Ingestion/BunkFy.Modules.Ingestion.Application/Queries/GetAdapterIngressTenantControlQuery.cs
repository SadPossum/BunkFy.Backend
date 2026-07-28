namespace BunkFy.Modules.Ingestion.Application.Queries;

using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetAdapterIngressTenantControlQuery
    : IQuery<AdapterIngressTenantControlDto>;
