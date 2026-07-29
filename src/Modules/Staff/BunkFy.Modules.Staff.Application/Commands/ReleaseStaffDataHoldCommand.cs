namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

public sealed record ReleaseStaffDataHoldCommand(
    Guid IdempotencyKey,
    Guid StaffMemberId,
    Guid HoldId,
    long ExpectedStaffVersion,
    long ExpectedHoldVersion,
    string ActorId)
    : ITransactionalCommand<StaffDataHoldReceiptDto>;
