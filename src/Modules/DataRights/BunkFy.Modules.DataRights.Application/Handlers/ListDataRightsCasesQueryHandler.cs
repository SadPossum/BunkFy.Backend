namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListDataRightsCasesQueryHandler(IDataRightsCaseRepository cases)
    : IQueryHandler<ListDataRightsCasesQuery, DataRightsCaseListResponse>
{
    public async Task<Result<DataRightsCaseListResponse>> HandleAsync(
        ListDataRightsCasesQuery query,
        CancellationToken cancellationToken) => Result.Success(await cases.ListAsync(
        query.PropertyId,
        query.Status,
        PageRequest.Normalize(query.Page, query.PageSize),
        cancellationToken).ConfigureAwait(false));
}
