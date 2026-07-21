namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;

internal sealed class GetStaffMemberAtPropertyQueryHandler(IStaffMemberRepository members)
    : IQueryHandler<GetStaffMemberAtPropertyQuery, StaffDirectoryMemberDto>
{
    public async Task<Result<StaffDirectoryMemberDto>> HandleAsync(GetStaffMemberAtPropertyQuery query,
        CancellationToken cancellationToken)
    {
        StaffDirectoryMemberDto? member = await members.GetDirectoryAtPropertyAsync(
            query.PropertyId,
            query.StaffMemberId,
            cancellationToken).ConfigureAwait(false);
        return member is null
            ? Result.Failure<StaffDirectoryMemberDto>(StaffApplicationErrors.StaffMemberNotFound)
            : Result.Success(member);
    }
}
