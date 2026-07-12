namespace BunkFy.Modules.Ingestion.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Contracts;

public sealed record GetLegalHoldQuery(Guid PropertyId, Guid HoldId)
    : IQuery<LegalHoldDto>;

public sealed record ListLegalHoldsQuery(
    Guid PropertyId,
    LegalHoldStatus? Status,
    int Page,
    int PageSize)
    : IQuery<LegalHoldListResponse>;
