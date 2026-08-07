namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;

public sealed record UnassignStaffPropertyCommand(Guid OperationId, Guid StaffMemberId, Guid PropertyId,
    DateOnly EffectiveTo, string Reason, long ExpectedVersion,
    string ActorId) : ITransactionalCommand<StaffMemberMutationReceiptDto>,
    IStaffPersistenceRetryableCommand;
