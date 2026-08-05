namespace BunkFy.Modules.Retention.Application.Queries;

using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;

public sealed record ListRetentionScheduleHealthQuery(
    int Page = PageRequest.DefaultPage,
    int PageSize = PageRequest.DefaultPageSize)
    : IQuery<RetentionScheduleHealthListResponse>;
