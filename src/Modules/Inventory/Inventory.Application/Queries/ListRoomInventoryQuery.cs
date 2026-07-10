namespace Inventory.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Inventory.Contracts;

public sealed record ListRoomInventoryQuery(
    Guid PropertyId,
    int Page = PageRequest.DefaultPage,
    int PageSize = PageRequest.DefaultPageSize)
    : IQuery<RoomInventoryListResponse>;
