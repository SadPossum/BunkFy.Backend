namespace BunkFy.Modules.Retention.Application.Queries;

using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;

public sealed record ListRetentionScheduleHealthQuery
    : IQuery<RetentionScheduleHealthListResponse>;
