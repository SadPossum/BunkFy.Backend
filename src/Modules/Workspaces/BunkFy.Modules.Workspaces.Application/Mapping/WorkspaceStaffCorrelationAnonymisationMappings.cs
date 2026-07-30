namespace BunkFy.Modules.Workspaces.Application.Mapping;

using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.DataRights;

internal static class
    WorkspaceStaffCorrelationAnonymisationMappings
{
    public static WorkspaceStaffCorrelationAnonymisationReceiptDto
        ToDto(
            this WorkspaceStaffCorrelationAnonymisationReceipt
                receipt) => new(
            receipt.ContractVersion,
            receipt.Id,
            receipt.IdempotencyKey,
            receipt.CaseId,
            receipt.ApprovalRevision,
            receipt.OperationRevision,
            receipt.AnchorProcessId,
            receipt.StaffMemberId,
            receipt.SelectedStaffVersion,
            receipt.SelectedAnchorVersion,
            receipt.ResultingAnchorVersion,
            receipt.OnboardingRecordsScrubbed,
            receipt.AccessProcessRecordsScrubbed,
            receipt.AccessPlanRecordsScrubbed,
            (Contracts
                .WorkspaceStaffCorrelationAnonymisationDisposition)
                receipt.Disposition,
            (Contracts
                .WorkspaceStaffCorrelationAnonymisationReason)
                receipt.Reason,
            receipt.ApprovalEvidenceSha256,
            receipt.StateBindingSha256,
            receipt.ResultingStateSha256,
            receipt.ActorId,
            receipt.CompletedAtUtc,
            receipt.CanonicalSha256);
}
