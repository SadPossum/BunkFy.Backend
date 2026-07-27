namespace BunkFy.Modules.Inventory.Application.Mapping;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.DataRights;
using ContractDisposition =
    BunkFy.Modules.Inventory.Contracts
        .InventoryAllocationAnonymisationDisposition;
using ContractReason =
    BunkFy.Modules.Inventory.Contracts
        .InventoryAllocationAnonymisationReason;

internal static class InventoryAllocationAnonymisationMappings
{
    public static InventoryAllocationAnonymisationReceiptDto ToDto(
        this InventoryAllocationAnonymisationReceipt receipt) => new(
        receipt.ContractVersion,
        receipt.Id,
        receipt.WorkItemId,
        receipt.IdempotencyKey,
        receipt.PropertyId,
        receipt.CaseId,
        receipt.ApprovalRevision,
        receipt.OperationRevision,
        receipt.AllocationId,
        receipt.SelectedAllocationVersion,
        receipt.ResultingAllocationVersion,
        receipt.ResultingReservationPseudonym,
        (ContractDisposition)receipt.Disposition,
        (ContractReason)receipt.Reason,
        receipt.RemovedAmendmentDecisionCount,
        receipt.ApprovalEvidenceSha256,
        receipt.ActorId,
        receipt.CompletedAtUtc,
        receipt.CanonicalSha256);
}
