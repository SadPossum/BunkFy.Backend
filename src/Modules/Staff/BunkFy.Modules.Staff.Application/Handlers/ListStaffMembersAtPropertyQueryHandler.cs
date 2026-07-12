namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;

internal sealed class ListStaffMembersAtPropertyQueryHandler(IStaffMemberRepository members)
    : IQueryHandler<ListStaffMembersAtPropertyQuery, StaffListResponse>
{
    public async Task<Result<StaffListResponse>> HandleAsync(ListStaffMembersAtPropertyQuery query,
        CancellationToken cancellationToken) => Result.Success(await members.ListAtPropertyAsync(query.PropertyId,
        query.Search, query.Status, PageRequest.Normalize(query.Page, query.PageSize), cancellationToken)
        .ConfigureAwait(false));
}
