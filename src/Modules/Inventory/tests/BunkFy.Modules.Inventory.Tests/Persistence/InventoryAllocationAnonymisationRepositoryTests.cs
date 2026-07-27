namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;
using BunkFy.Modules.Inventory.Persistence;
using BunkFy.Modules.Inventory.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    InventoryAllocationAnonymisationRepositoryTests
{
    private const string ScopeId = "tenant-a";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Owner_state_persists_with_decisions_removed()
    {
        await using InventoryDbContext dbContext = CreateDbContext();
        InventoryAllocation allocation = CreateRejected();
        dbContext.Allocations.Add(allocation);
        dbContext.AllocationAmendmentDecisions.Add(
            new(new InventoryAllocationAmendmentDecisionRecord(
                Guid.NewGuid(),
                ScopeId,
                allocation.Id,
                allocation.ReservationId,
                allocation.PropertyId,
                new string('d', 64),
                Confirmed: false,
                InventoryAllocationRejectionReason
                    .AllocationConflict,
                AllocationVersion: null,
                Now.AddMinutes(-1))));
        await dbContext.SaveChangesAsync();
        InventoryAllocationAnonymisationRepository repository =
            new(dbContext);
        Guid receiptId = Guid.NewGuid();
        InventoryAllocationAnonymisationOutcome outcome =
            allocation.Anonymise(
                allocation.Version,
                Guid.NewGuid(),
                Now).Value;
        int removed =
            await repository.RemoveAmendmentDecisionsAsync(
                allocation.PropertyId,
                allocation.Id,
                CancellationToken.None);
        InventoryAllocationAnonymisationReceipt receipt =
            InventoryAllocationAnonymisationReceipt.Create(
                receiptId,
                ScopeId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                allocation.PropertyId,
                Guid.NewGuid(),
                approvalRevision: 3,
                operationRevision: 4,
                allocation.Id,
                outcome,
                removed,
                new string('a', 64),
                "user:privacy-executor").Value;
        InventoryAllocationAnonymisationTombstone tombstone =
            InventoryAllocationAnonymisationTombstone.Create(
                receipt).Value;
        await repository.AddOwnerProofAsync(
            receipt,
            tombstone,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        InventoryAllocationAnonymisationReceipt? persisted =
            await repository.FindReceiptByIdempotencyKeyAsync(
                receipt.IdempotencyKey,
                CancellationToken.None);
        bool proven = await repository.VerifyOwnerStateAsync(
            persisted!,
            CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.NotNull(persisted);
        Assert.Equal(receipt.CanonicalSha256,
            persisted.CanonicalSha256);
        Assert.True(proven);
        Assert.Empty(
            await dbContext.AllocationAmendmentDecisions
                .AsNoTracking()
                .ToArrayAsync());
    }

    [Fact]
    public async Task Operation_lock_is_singleton_and_monotonic()
    {
        await using InventoryDbContext dbContext = CreateDbContext();
        InventoryAllocationOperationLockRepository repository =
            new(dbContext);
        Guid allocationId = Guid.NewGuid();

        await repository.AcquireAsync(
            ScopeId,
            allocationId,
            CancellationToken.None);
        await repository.AcquireAsync(
            ScopeId,
            allocationId,
            CancellationToken.None);

        InventoryAllocationOperationLock resourceLock =
            Assert.Single(
                await dbContext.AllocationOperationLocks
                    .AsNoTracking()
                    .ToArrayAsync());
        Assert.Equal(allocationId, resourceLock.AllocationId);
        Assert.Equal(2, resourceLock.Revision);
    }

    private static InventoryAllocation CreateRejected() =>
        InventoryAllocation.CreateRejected(
            Guid.NewGuid(),
            ScopeId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            InventoryAllocationRejection.AllocationConflict,
            Now.AddDays(-1)).Value;

    private static InventoryDbContext CreateDbContext()
    {
        DbContextOptions<InventoryDbContext> options =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(
                    $"inventory-anonymisation-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => InventoryAllocationAnonymisationRepositoryTests.ScopeId;
    }
}
