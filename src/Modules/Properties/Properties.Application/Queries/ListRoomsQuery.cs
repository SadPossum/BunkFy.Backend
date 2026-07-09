namespace Properties.Application.Queries;

using Properties.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;

public sealed record ListRoomsQuery(
    Guid PropertyId,
    int Page = PageRequest.DefaultPage,
    int PageSize = PageRequest.DefaultPageSize)
    : IQuery<RoomListResponse>;
