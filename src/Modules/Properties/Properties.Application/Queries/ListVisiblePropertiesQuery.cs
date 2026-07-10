namespace Properties.Application.Queries;

using Properties.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;

public sealed record ListVisiblePropertiesQuery(
    AccessSubject Subject,
    int Page = PageRequest.DefaultPage,
    int PageSize = PageRequest.DefaultPageSize)
    : IQuery<PropertyListResponse>;
