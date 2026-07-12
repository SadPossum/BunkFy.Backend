namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Application.Queries;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListBedsQueryHandler(IPropertiesReadRepository repository)
    : IQueryHandler<ListBedsQuery, BedListResponse>
{
    public async Task<Result<BedListResponse>> HandleAsync(ListBedsQuery query, CancellationToken cancellationToken)
    {
        PageRequest pageRequest = PageRequest.Normalize(query.Page, query.PageSize);
        RoomDto? room = await repository.GetRoomAsync(query.PropertyId, query.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure<BedListResponse>(PropertiesApplicationErrors.RoomNotFound);
        }

        return Result.Success(await repository.ListBedsAsync(query.PropertyId, query.RoomId, pageRequest, cancellationToken).ConfigureAwait(false));
    }
}
