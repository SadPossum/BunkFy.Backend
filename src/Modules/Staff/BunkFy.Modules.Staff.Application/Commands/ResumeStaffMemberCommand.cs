namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;

public sealed record ResumeStaffMemberCommand(Guid OperationId, Guid StaffMemberId,
    string Reason, long ExpectedVersion, string ActorId)
    : ITransactionalCommand<StaffMemberMutationReceiptDto>,
    IStaffPersistenceRetryableCommand;
