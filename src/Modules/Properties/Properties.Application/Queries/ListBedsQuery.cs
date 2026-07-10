namespace Properties.Application.Queries;

using Properties.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;

public sealed record ListBedsQuery(
    Guid PropertyId,
    Guid RoomId,
    int Page = PageRequest.DefaultPage,
    int PageSize = PageRequest.DefaultPageSize)
    : IQuery<BedListResponse>;
