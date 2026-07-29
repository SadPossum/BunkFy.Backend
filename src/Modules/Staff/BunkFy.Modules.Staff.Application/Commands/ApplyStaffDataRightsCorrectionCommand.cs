namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyStaffDataRightsCorrectionCommand(
    Guid ExecutionId,
    Guid CaseId,
    long ApprovalRevision,
    Guid StaffMemberId,
    long ExpectedVersion,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department,
    string ActorId)
    : ITransactionalCommand<StaffDataRightsCorrectionReceiptDto>;
