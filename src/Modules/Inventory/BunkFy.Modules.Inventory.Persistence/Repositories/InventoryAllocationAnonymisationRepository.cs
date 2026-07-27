namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryAllocationAnonymisationRepository(
    InventoryDbContext dbContext)
    : IInventoryAllocationAnonymisationRepository,
        IInventoryAllocationAnonymisationRestoreRepository
{
    public Task<InventoryAllocationAnonymisationReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
        dbContext.AllocationAnonymisationReceipts
            .SingleOrDefaultAsync(
                receipt =>
                    receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<InventoryAllocation?> GetAllocationAsync(
        Guid propertyId,
        Guid allocationId,
        CancellationToken cancellationToken) =>
        dbContext.Allocations
            .Include(allocation => allocation.Units)
            .SingleOrDefaultAsync(
                allocation =>
                    allocation.PropertyId == propertyId &&
                    allocation.Id == allocationId,
                cancellationToken);

    public async Task<int> RemoveAmendmentDecisionsAsync(
        Guid propertyId,
        Guid allocationId,
        CancellationToken cancellationToken)
    {
        InventoryAllocationAmendmentDecision[] decisions =
            await dbContext.AllocationAmendmentDecisions
                .Where(decision =>
                    decision.PropertyId == propertyId &&
                    decision.AllocationId == allocationId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        dbContext.AllocationAmendmentDecisions.RemoveRange(
            decisions);
        return decisions.Length;
    }

    public async Task<bool> VerifyOwnerStateAsync(
        InventoryAllocationAnonymisationReceipt receipt,
        CancellationToken cancellationToken)
    {
        InventoryAllocationAnonymisationTombstone? tombstone =
            await dbContext.AllocationAnonymisationTombstones
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == receipt.AllocationId,
                    cancellationToken)
                .ConfigureAwait(false);
        InventoryAllocation? allocation =
            await dbContext.Allocations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.PropertyId == receipt.PropertyId &&
                        item.Id == receipt.AllocationId,
                    cancellationToken)
                .ConfigureAwait(false);
        bool hasDecisions =
            await dbContext.AllocationAmendmentDecisions
                .AsNoTracking()
                .AnyAsync(
                    decision =>
                        decision.PropertyId == receipt.PropertyId &&
                        decision.AllocationId ==
                            receipt.AllocationId,
                    cancellationToken)
                .ConfigureAwait(false);
        return tombstone is not null &&
            tombstone.Matches(receipt) &&
            allocation is not null &&
            allocation.MatchesAnonymisedState(
                receipt.ResultingAllocationVersion,
                receipt.ResultingReservationPseudonym,
                receipt.CompletedAtUtc) &&
            !hasDecisions;
    }

    public Task AddOwnerProofAsync(
        InventoryAllocationAnonymisationReceipt receipt,
        InventoryAllocationAnonymisationTombstone tombstone,
        CancellationToken cancellationToken)
    {
        dbContext.AllocationAnonymisationReceipts.Add(receipt);
        dbContext.AllocationAnonymisationTombstones.Add(tombstone);
        return Task.CompletedTask;
    }

    public Task<InventoryAllocationAnonymisationReceipt?>
        GetOriginalReceiptAsync(
            Guid receiptId,
            CancellationToken cancellationToken) =>
        dbContext.AllocationAnonymisationReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.Id == receiptId,
                cancellationToken);

    public Task<InventoryAllocationAnonymisationTombstone?>
        GetTombstoneAsync(
            Guid allocationId,
            CancellationToken cancellationToken) =>
        dbContext.AllocationAnonymisationTombstones
            .SingleOrDefaultAsync(
                tombstone => tombstone.Id == allocationId,
                cancellationToken);

    public Task<InventoryAllocationAnonymisationRestoreReceipt?>
        GetRestoreReceiptAsync(
            Guid ledgerEntryId,
            CancellationToken cancellationToken) =>
        dbContext.AllocationAnonymisationRestoreReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt =>
                    receipt.LedgerEntryId == ledgerEntryId,
                cancellationToken);

    public async Task<bool> VerifyRestoredOwnerStateAsync(
        Guid propertyId,
        Guid allocationId,
        Guid reservationPseudonym,
        bool allocationPresent,
        long resultingAllocationVersion,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        InventoryAllocation? allocation =
            await dbContext.Allocations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.PropertyId == propertyId &&
                        item.Id == allocationId,
                    cancellationToken)
                .ConfigureAwait(false);
        bool allocationMatches = allocationPresent
            ? allocation is not null &&
              allocation.MatchesAnonymisedState(
                  resultingAllocationVersion,
                  reservationPseudonym,
                  completedAtUtc)
            : allocation is null;
        bool hasDecisions =
            await dbContext.AllocationAmendmentDecisions
                .AsNoTracking()
                .AnyAsync(
                    decision =>
                        decision.PropertyId == propertyId &&
                        decision.AllocationId == allocationId,
                    cancellationToken)
                .ConfigureAwait(false);
        return allocationMatches && !hasDecisions;
    }

    public Task AddRestoreProofAsync(
        InventoryAllocationAnonymisationRestoreReceipt receipt,
        InventoryAllocationAnonymisationTombstone? newTombstone,
        CancellationToken cancellationToken)
    {
        if (newTombstone is not null)
        {
            dbContext.AllocationAnonymisationTombstones.Add(
                newTombstone);
        }

        dbContext.AllocationAnonymisationRestoreReceipts.Add(
            receipt);
        return Task.CompletedTask;
    }
}
