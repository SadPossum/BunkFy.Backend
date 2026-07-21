namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetStaffDirectoryMemberQueryHandler(IStaffMemberRepository members)
    : IQueryHandler<GetStaffDirectoryMemberQuery, StaffDirectoryMemberDto>
{
    public async Task<Result<StaffDirectoryMemberDto>> HandleAsync(
        GetStaffDirectoryMemberQuery query,
        CancellationToken cancellationToken)
    {
        StaffDirectoryMemberDto? member = await members
            .GetDirectoryAsync(query.StaffMemberId, cancellationToken)
            .ConfigureAwait(false);
        return member is null
            ? Result.Failure<StaffDirectoryMemberDto>(StaffApplicationErrors.StaffMemberNotFound)
            : Result.Success(member);
    }
}
