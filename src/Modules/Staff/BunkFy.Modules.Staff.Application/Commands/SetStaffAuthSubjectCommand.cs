namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;

public sealed record SetStaffAuthSubjectCommand(Guid StaffMemberId, string? AuthSubjectId,
    long ExpectedVersion, string ActorId) : ITransactionalCommand<StaffDirectoryMemberDto>,
    IStaffPersistenceRetryableCommand;
