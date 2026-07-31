namespace BunkFy.Modules.Workspaces.Application.Mapping;

using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.Termination;

internal static class WorkspaceTerminationFenceMappings
{
    public static WorkspaceTerminationFenceReceiptDto ToDto(
        this WorkspaceTerminationFenceReceipt receipt) =>
        new(
            receipt.Id,
            receipt.FenceId,
            receipt.ProcessId,
            receipt.CaseId,
            receipt.ApprovalRevision,
            receipt.OperationRevision,
            receipt.WorkItemId,
            receipt.IdempotencyKey,
            receipt.TerminationEpoch,
            (WorkspaceTerminationFenceActionDto)receipt.Action,
            receipt.SelectedFenceVersion,
            receipt.ResultingFenceVersion,
            (WorkspaceTerminationFenceReceiptStateDto)receipt.ResultingState,
            receipt.PolicyEvidenceSha256,
            receipt.ActorId,
            receipt.CompletedAtUtc);
}
