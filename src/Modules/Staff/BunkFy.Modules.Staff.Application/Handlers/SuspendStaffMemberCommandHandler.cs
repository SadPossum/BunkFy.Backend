namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;

internal sealed class SuspendStaffMemberCommandHandler(IStaffMemberRepository members,
    ISystemClock clock, IIdGenerator ids)
    : ICommandHandler<SuspendStaffMemberCommand, StaffDirectoryMemberDto>
{
    public Task<Result<StaffDirectoryMemberDto>> HandleAsync(SuspendStaffMemberCommand command,
        CancellationToken cancellationToken) => StaffMemberChange.ApplyAsync(members, command.StaffMemberId,
        member => member.Suspend(command.ExpectedVersion, command.ActorId, command.Reason, ids.NewId(), clock.UtcNow),
        cancellationToken);
}
