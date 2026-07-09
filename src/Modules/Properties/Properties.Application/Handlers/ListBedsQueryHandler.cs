namespace Properties.Application.Handlers;

using Properties.Application.Ports;
using Properties.Application.Queries;
using Properties.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListBedsQueryHandler(IPropertiesReadRepository repository)
    : IQueryHandler<ListBedsQuery, BedListResponse>
{
    public async Task<Result<BedListResponse>> HandleAsync(ListBedsQuery query, CancellationToken cancellationToken)
    {
        PageRequest pageRequest = PageRequest.Normalize(query.Page, query.PageSize);
        RoomDto? room = await repository.GetRoomAsync(query.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure<BedListResponse>(PropertiesApplicationErrors.RoomNotFound);
        }

        return Result.Success(await repository.ListBedsAsync(query.RoomId, pageRequest, cancellationToken).ConfigureAwait(false));
    }
}
