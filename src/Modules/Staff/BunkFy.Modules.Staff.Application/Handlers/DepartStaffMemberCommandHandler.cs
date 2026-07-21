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

internal sealed class DepartStaffMemberCommandHandler(IStaffMemberRepository members,
    ISystemClock clock, IIdGenerator ids)
    : ICommandHandler<DepartStaffMemberCommand, StaffDirectoryMemberDto>
{
    public async Task<Result<StaffDirectoryMemberDto>> HandleAsync(DepartStaffMemberCommand command,
        CancellationToken cancellationToken)
    {
        StaffMember? member = await members.GetAsync(command.StaffMemberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffDirectoryMemberDto>(StaffApplicationErrors.StaffMemberNotFound);
        }

        Guid[] assignmentEventIds = member.Assignments.Where(item => item.IsCurrent)
            .Select(_ => ids.NewId()).ToArray();
        Result departed = member.Depart(command.EffectiveOn, command.ExpectedVersion, command.ActorId,
            command.Reason, ids.NewId(), assignmentEventIds, clock.UtcNow);
        return departed.IsSuccess
            ? Result.Success(member.ToDirectoryDto())
            : Result.Failure<StaffDirectoryMemberDto>(departed.Error);
    }
}
