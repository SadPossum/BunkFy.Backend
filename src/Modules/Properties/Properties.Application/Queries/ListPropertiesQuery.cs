namespace Properties.Application.Queries;

using Properties.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;

public sealed record ListPropertiesQuery(
    int Page = PageRequest.DefaultPage,
    int PageSize = PageRequest.DefaultPageSize)
    : IQuery<PropertyListResponse>;
