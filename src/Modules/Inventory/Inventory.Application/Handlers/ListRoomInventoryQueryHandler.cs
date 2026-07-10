namespace Inventory.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Inventory.Application.Ports;
using Inventory.Application.Queries;
using Inventory.Contracts;

internal sealed class ListRoomInventoryQueryHandler(IInventoryReadRepository repository)
    : IQueryHandler<ListRoomInventoryQuery, RoomInventoryListResponse>
{
    public async Task<Result<RoomInventoryListResponse>> HandleAsync(
        ListRoomInventoryQuery query,
        CancellationToken cancellationToken)
    {
        if (!await repository.PropertyExistsAsync(query.PropertyId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<RoomInventoryListResponse>(InventoryApplicationErrors.PropertyNotFound);
        }

        PageRequest pageRequest = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await repository
            .ListRoomsAsync(query.PropertyId, pageRequest, cancellationToken)
            .ConfigureAwait(false));
    }
}
