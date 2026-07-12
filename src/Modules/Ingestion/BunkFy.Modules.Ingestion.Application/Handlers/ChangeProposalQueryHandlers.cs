namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;

internal sealed class GetChangeProposalQueryHandler(IChangeProposalReader proposals)
    : IQueryHandler<GetChangeProposalQuery, ChangeProposalDto>
{
    public async Task<Result<ChangeProposalDto>> HandleAsync(
        GetChangeProposalQuery query,
        CancellationToken cancellationToken)
    {
        ChangeProposalDto? proposal = await proposals.GetAsync(
            query.PropertyId,
            query.ProposalId,
            cancellationToken).ConfigureAwait(false);
        return proposal is null
            ? Result.Failure<ChangeProposalDto>(IngestionApplicationErrors.ProposalNotFound)
            : Result.Success(proposal);
    }
}

internal sealed class ListChangeProposalsQueryHandler(IChangeProposalReader proposals)
    : IQueryHandler<ListChangeProposalsQuery, ChangeProposalListResponse>
{
    public async Task<Result<ChangeProposalListResponse>> HandleAsync(
        ListChangeProposalsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            return Result.Failure<ChangeProposalListResponse>(IngestionApplicationErrors.ProposalStatusInvalid);
        }

        return Result.Success(await proposals.ListAsync(
                query.PropertyId,
                query.Status,
                PageRequest.Normalize(query.Page, query.PageSize),
                cancellationToken)
            .ConfigureAwait(false));
    }
}
