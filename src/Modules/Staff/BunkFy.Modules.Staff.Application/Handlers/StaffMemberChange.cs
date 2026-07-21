namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Results;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal static class StaffMemberChange
{
    public static async Task<Result<StaffDirectoryMemberDto>> ApplyAsync(IStaffMemberRepository members,
        Guid staffMemberId, Func<StaffMember, Result> change, CancellationToken cancellationToken)
    {
        StaffMember? member = await members.GetAsync(staffMemberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffDirectoryMemberDto>(StaffApplicationErrors.StaffMemberNotFound);
        }

        Result changed = change(member);
        return changed.IsSuccess
            ? Result.Success(member.ToDirectoryDto())
            : Result.Failure<StaffDirectoryMemberDto>(changed.Error);
    }
}
