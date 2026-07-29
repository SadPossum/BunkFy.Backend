namespace BunkFy.Modules.Staff.Application.Queries;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

public sealed record ListStaffDataHoldsQuery(
    Guid StaffMemberId,
    StaffDataHoldStatus? Status,
    int Page,
    int PageSize)
    : IQuery<StaffDataHoldListResponse>;
