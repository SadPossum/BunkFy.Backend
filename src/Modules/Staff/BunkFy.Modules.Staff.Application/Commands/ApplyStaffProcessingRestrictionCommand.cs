namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyStaffProcessingRestrictionCommand(
    Guid IdempotencyKey,
    Guid CaseId,
    long ApprovalRevision,
    Guid StaffMemberId,
    long ExpectedStaffVersion,
    long ExpectedProjectionRevision,
    string ActorId)
    : ITransactionalCommand<StaffProcessingRestrictionReceiptDto>;
