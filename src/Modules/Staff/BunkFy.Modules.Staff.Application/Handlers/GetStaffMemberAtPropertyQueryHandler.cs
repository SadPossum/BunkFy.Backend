namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class GetStaffMemberAtPropertyQueryHandler(IStaffMemberRepository members)
    : IQueryHandler<GetStaffMemberAtPropertyQuery, StaffMemberDto>
{
    public async Task<Result<StaffMemberDto>> HandleAsync(GetStaffMemberAtPropertyQuery query,
        CancellationToken cancellationToken)
    {
        StaffMember? member = await members.GetAtPropertyAsync(query.PropertyId, query.StaffMemberId,
            cancellationToken).ConfigureAwait(false);
        return member is null
            ? Result.Failure<StaffMemberDto>(StaffApplicationErrors.StaffMemberNotFound)
            : Result.Success(member.ToDto());
    }
}
