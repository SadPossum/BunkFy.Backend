namespace BunkFy.Modules.Ingestion.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Contracts;

public sealed record GetChangeProposalQuery(Guid PropertyId, Guid ProposalId)
    : IQuery<ChangeProposalDto>;

public sealed record ListChangeProposalsQuery(
    Guid PropertyId,
    ChangeProposalStatus? Status,
    int Page,
    int PageSize)
    : IQuery<ChangeProposalListResponse>;
