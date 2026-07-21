namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class UnassignStaffPropertyCommandHandler(IStaffMemberRepository members,
    ISystemClock clock, IIdGenerator ids)
    : ICommandHandler<UnassignStaffPropertyCommand, StaffDirectoryMemberDto>
{
    public async Task<Result<StaffDirectoryMemberDto>> HandleAsync(UnassignStaffPropertyCommand command,
        CancellationToken cancellationToken)
    {
        StaffMember? member = await members.GetAsync(command.StaffMemberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffDirectoryMemberDto>(StaffApplicationErrors.StaffMemberNotFound);
        }

        Result unassigned = member.UnassignProperty(command.PropertyId, command.EffectiveTo,
            command.ExpectedVersion, command.ActorId, command.Reason, ids.NewId(), clock.UtcNow);
        return unassigned.IsSuccess
            ? Result.Success(member.ToDirectoryDto(command.PropertyId))
            : Result.Failure<StaffDirectoryMemberDto>(unassigned.Error);
    }
}
