namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;

public sealed record AssignStaffPropertyCommand(Guid OperationId, Guid StaffMemberId, Guid PropertyId,
    string? PropertyJobTitle, bool IsPrimary, DateOnly EffectiveFrom,
    long ExpectedVersion, string ActorId) : ITransactionalCommand<StaffMemberMutationReceiptDto>,
    IStaffPersistenceRetryableCommand;
