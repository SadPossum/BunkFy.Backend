namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

internal sealed record ApplyStaffAnonymisationCommand(
    Guid IdempotencyKey,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid StaffMemberId,
    long ExpectedStaffVersion,
    DataRightsApprovalEvidence ApprovalEvidence,
    string ActorId)
    : ITransactionalCommand<StaffAnonymisationReceiptDto>;
