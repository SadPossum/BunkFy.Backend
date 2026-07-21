namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;

internal sealed class ResumeStaffMemberCommandHandler(IStaffMemberRepository members,
    ISystemClock clock, IIdGenerator ids)
    : ICommandHandler<ResumeStaffMemberCommand, StaffDirectoryMemberDto>
{
    public Task<Result<StaffDirectoryMemberDto>> HandleAsync(ResumeStaffMemberCommand command,
        CancellationToken cancellationToken) => StaffMemberChange.ApplyAsync(members, command.StaffMemberId,
        member => member.Resume(command.ExpectedVersion, command.ActorId, command.Reason, ids.NewId(), clock.UtcNow),
        cancellationToken);
}
