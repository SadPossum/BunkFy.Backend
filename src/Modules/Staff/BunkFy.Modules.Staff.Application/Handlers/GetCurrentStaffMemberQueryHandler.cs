namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetCurrentStaffMemberQueryHandler(IStaffMemberRepository members)
    : IQueryHandler<GetCurrentStaffMemberQuery, StaffMemberDto>
{
    public async Task<Result<StaffMemberDto>> HandleAsync(
        GetCurrentStaffMemberQuery query,
        CancellationToken cancellationToken)
    {
        StaffMember? member = await members
            .GetByAuthSubjectAsync(query.AuthSubjectId, cancellationToken)
            .ConfigureAwait(false);
        return member is null
            ? Result.Failure<StaffMemberDto>(StaffApplicationErrors.StaffMemberNotFound)
            : Result.Success(member.ToDto());
    }
}
