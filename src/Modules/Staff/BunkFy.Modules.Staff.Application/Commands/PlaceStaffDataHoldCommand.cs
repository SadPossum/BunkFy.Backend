namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

public sealed record PlaceStaffDataHoldCommand(
    Guid IdempotencyKey,
    Guid StaffMemberId,
    long ExpectedStaffVersion,
    string ReasonCode,
    string ActorId)
    : ITransactionalCommand<StaffDataHoldReceiptDto>;
