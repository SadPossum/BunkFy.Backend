namespace BunkFy.Modules.Inventory.Contracts;

public sealed record InventoryAllocationAnonymisationReceiptDto(
    int ContractVersion,
    Guid ReceiptId,
    Guid WorkItemId,
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid AllocationId,
    long SelectedAllocationVersion,
    long ResultingAllocationVersion,
    Guid ResultingReservationPseudonym,
    InventoryAllocationAnonymisationDisposition Disposition,
    InventoryAllocationAnonymisationReason Reason,
    int RemovedAmendmentDecisionCount,
    string ApprovalEvidenceSha256,
    string ActorId,
    DateTimeOffset CompletedAtUtc,
    string CanonicalSha256);

public enum InventoryAllocationAnonymisationDisposition
{
    Unknown = 0,
    Completed = 1
}

public enum InventoryAllocationAnonymisationReason
{
    Unknown = 0,
    ReservationCorrelationPseudonymised = 1
}

public static class InventoryAllocationAnonymisationContract
{
    public const int CurrentVersion = 1;
    public const int Sha256Length = 64;
}
