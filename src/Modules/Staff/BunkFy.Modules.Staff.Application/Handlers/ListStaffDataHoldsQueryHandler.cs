namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListStaffDataHoldsQueryHandler(
    IStaffMemberRepository members,
    IStaffDataHoldRepository holds)
    : IQueryHandler<ListStaffDataHoldsQuery, StaffDataHoldListResponse>
{
    public async Task<Result<StaffDataHoldListResponse>> HandleAsync(
        ListStaffDataHoldsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.StaffMemberId == Guid.Empty ||
            (query.Status.HasValue &&
             query.Status is not StaffDataHoldStatus.Active and
                 not StaffDataHoldStatus.Released))
        {
            return Result.Failure<StaffDataHoldListResponse>(
                StaffApplicationErrors.DataHoldRequestInvalid);
        }

        StaffMember? member = await members.GetForDataRightsAsync(
            query.StaffMemberId,
            cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffDataHoldListResponse>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        PageRequest page =
            PageRequest.Normalize(query.Page, query.PageSize);
        IReadOnlyCollection<StaffDataHold> rows =
            await holds.ListAsync(
                query.StaffMemberId,
                query.Status,
                page,
                cancellationToken).ConfigureAwait(false);
        long total = await holds.CountAsync(
            query.StaffMemberId,
            query.Status,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new StaffDataHoldListResponse(
            rows.Select(hold => hold.ToDto()).ToArray(),
            total,
            page.Page,
            page.PageSize));
    }
}
