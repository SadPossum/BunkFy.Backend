namespace BunkFy.Modules.Staff.Application.Mapping;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;

internal static class StaffAnonymisationMappings
{
    public static StaffAnonymisationReceiptDto ToDto(
        this StaffAnonymisationReceipt receipt) => new(
        receipt.ContractVersion,
        receipt.Id,
        receipt.IdempotencyKey,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.OperationRevision,
        receipt.StaffMemberId,
        receipt.SelectedStaffVersion,
        receipt.ResultingStaffVersion,
        receipt.SelectedOperationLockRevision,
        receipt.ResultingOperationLockRevision,
        (StaffAnonymisationDisposition)receipt.Disposition,
        (StaffAnonymisationReason)receipt.Reason,
        receipt.ApprovalEvidenceSha256,
        receipt.StateBindingsSha256,
        receipt.EventId,
        receipt.ActorId,
        receipt.CompletedAtUtc,
        receipt.CanonicalSha256);
}
