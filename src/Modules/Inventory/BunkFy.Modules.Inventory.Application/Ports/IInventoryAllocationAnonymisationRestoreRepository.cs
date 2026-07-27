namespace BunkFy.Modules.Inventory.Application.Ports;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;

public interface IInventoryAllocationAnonymisationRestoreRepository
{
    Task<InventoryAllocation?> GetAllocationAsync(
        Guid propertyId,
        Guid allocationId,
        CancellationToken cancellationToken);

    Task<InventoryAllocationAnonymisationReceipt?>
        GetOriginalReceiptAsync(
            Guid receiptId,
            CancellationToken cancellationToken);

    Task<InventoryAllocationAnonymisationTombstone?>
        GetTombstoneAsync(
            Guid allocationId,
            CancellationToken cancellationToken);

    Task<InventoryAllocationAnonymisationRestoreReceipt?>
        GetRestoreReceiptAsync(
            Guid ledgerEntryId,
            CancellationToken cancellationToken);

    Task<int> RemoveAmendmentDecisionsAsync(
        Guid propertyId,
        Guid allocationId,
        CancellationToken cancellationToken);

    Task<bool> VerifyRestoredOwnerStateAsync(
        Guid propertyId,
        Guid allocationId,
        Guid reservationPseudonym,
        bool allocationPresent,
        long resultingAllocationVersion,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken);

    Task AddRestoreProofAsync(
        InventoryAllocationAnonymisationRestoreReceipt receipt,
        InventoryAllocationAnonymisationTombstone? newTombstone,
        CancellationToken cancellationToken);
}
