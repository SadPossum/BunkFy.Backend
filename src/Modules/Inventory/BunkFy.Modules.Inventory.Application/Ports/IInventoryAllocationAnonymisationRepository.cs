namespace BunkFy.Modules.Inventory.Application.Ports;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;

public interface IInventoryAllocationAnonymisationRepository
{
    Task<InventoryAllocationAnonymisationReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken);

    Task<InventoryAllocation?> GetAllocationAsync(
        Guid propertyId,
        Guid allocationId,
        CancellationToken cancellationToken);

    Task<int> RemoveAmendmentDecisionsAsync(
        Guid propertyId,
        Guid allocationId,
        CancellationToken cancellationToken);

    Task<bool> VerifyOwnerStateAsync(
        InventoryAllocationAnonymisationReceipt receipt,
        CancellationToken cancellationToken);

    Task AddOwnerProofAsync(
        InventoryAllocationAnonymisationReceipt receipt,
        InventoryAllocationAnonymisationTombstone tombstone,
        CancellationToken cancellationToken);
}
