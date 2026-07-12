namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Application.Queries;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListPropertiesQueryHandler(IPropertiesReadRepository repository)
    : IQueryHandler<ListPropertiesQuery, PropertyListResponse>
{
    public async Task<Result<PropertyListResponse>> HandleAsync(
        ListPropertiesQuery query,
        CancellationToken cancellationToken)
    {
        PageRequest pageRequest = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await repository.ListPropertiesAsync(pageRequest, cancellationToken).ConfigureAwait(false));
    }
}
