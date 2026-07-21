namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Contracts;

public sealed record SuspendStaffMemberCommand(Guid StaffMemberId, string Reason,
    long ExpectedVersion, string ActorId) : ITransactionalCommand<StaffDirectoryMemberDto>;
