namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Application.Policies;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    RestoreInventoryAllocationAnonymisationCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Restore_replays_allocation_state_and_owner_proof()
    {
        InventoryAllocation allocation = CreateReleased();
        RecordingRestoreRepository repository = new(allocation);
        RecordingOperationLock operationLock = new();
        RestoreInventoryAllocationAnonymisationCommandHandler handler =
            new(
                repository,
                operationLock,
                new TestScopeContext(),
                new TestClock());
        DataRightsAnonymisationRestoreRequest request =
            CreateRequest(allocation.Id, allocation.PropertyId, 3);

        Result<InventoryAllocationAnonymisationRestoreReceipt> first =
            await handler.HandleAsync(
                new(request),
                CancellationToken.None);
        Result<InventoryAllocationAnonymisationRestoreReceipt> replay =
            await handler.HandleAsync(
                new(request),
                CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value.CanonicalSha256,
            replay.Value.CanonicalSha256);
        Assert.True(first.Value.AllocationPresent);
        Assert.True(allocation.MatchesAnonymisedState(
            3,
            first.Value.ResultingReservationPseudonym,
            request.OriginallyCompletedAtUtc));
        Assert.Equal(2, operationLock.AllocationIds.Count);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(1, repository.RemoveCount);
    }

    [Fact]
    public async Task Restore_after_retention_proves_absence_without_recreation()
    {
        Guid allocationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        RecordingRestoreRepository repository = new(allocation: null);
        RestoreInventoryAllocationAnonymisationCommandHandler handler =
            new(
                repository,
                new RecordingOperationLock(),
                new TestScopeContext(),
                new TestClock());

        Result<InventoryAllocationAnonymisationRestoreReceipt> result =
            await handler.HandleAsync(
                new(CreateRequest(
                    allocationId,
                    propertyId,
                    resultingVersion: 7)),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.AllocationPresent);
        Assert.NotNull(repository.Tombstone);
        Assert.False(repository.Tombstone.AllocationPresent);
        Assert.Null(repository.Allocation);
    }

    private static DataRightsAnonymisationRestoreRequest CreateRequest(
        Guid allocationId,
        Guid propertyId,
        long resultingVersion) =>
        new(
            DataRightsAnonymisationRestoreContract.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            TenantSequence: 9,
            new string('b', 64),
            propertyId,
            InventoryDataRightsCoordinates.Owner,
            InventoryDataRightsCoordinates.AllocationRecordType,
            allocationId,
            OwnerReceiptContractVersion: 1,
            Guid.NewGuid(),
            new string('c', 64),
            resultingVersion,
            Now.AddMinutes(-1));

    private static InventoryAllocation CreateReleased()
    {
        InventoryAllocation allocation =
            InventoryAllocation.CreateAccepted(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 3),
                [Guid.NewGuid()],
                Now.AddDays(-1)).Value;
        Assert.True(allocation.Release(
            Guid.NewGuid(),
            allocation.Version,
            Now.AddHours(-1)).IsSuccess);
        return allocation;
    }

    private sealed class RecordingRestoreRepository(
        InventoryAllocation? allocation)
        : IInventoryAllocationAnonymisationRestoreRepository
    {
        public InventoryAllocation? Allocation { get; } = allocation;
        public InventoryAllocationAnonymisationTombstone? Tombstone
        {
            get;
            private set;
        }

        public InventoryAllocationAnonymisationRestoreReceipt?
            RestoreReceipt
        { get; private set; }

        public int AddCount { get; private set; }
        public int RemoveCount { get; private set; }

        public Task<InventoryAllocation?> GetAllocationAsync(
            Guid propertyId,
            Guid allocationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<InventoryAllocation?>(
                this.Allocation?.PropertyId == propertyId &&
                this.Allocation.Id == allocationId
                    ? this.Allocation
                    : null);

        public Task<InventoryAllocationAnonymisationReceipt?>
            GetOriginalReceiptAsync(
                Guid receiptId,
                CancellationToken cancellationToken) =>
            Task.FromResult<
                InventoryAllocationAnonymisationReceipt?>(null);

        public Task<InventoryAllocationAnonymisationTombstone?>
            GetTombstoneAsync(
                Guid allocationId,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Tombstone?.Id == allocationId
                    ? this.Tombstone
                    : null);

        public Task<
            InventoryAllocationAnonymisationRestoreReceipt?>
            GetRestoreReceiptAsync(
                Guid ledgerEntryId,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                this.RestoreReceipt?.LedgerEntryId == ledgerEntryId
                    ? this.RestoreReceipt
                    : null);

        public Task<int> RemoveAmendmentDecisionsAsync(
            Guid propertyId,
            Guid allocationId,
            CancellationToken cancellationToken)
        {
            this.RemoveCount++;
            return Task.FromResult(0);
        }

        public Task<bool> VerifyRestoredOwnerStateAsync(
            Guid propertyId,
            Guid allocationId,
            Guid reservationPseudonym,
            bool allocationPresent,
            long resultingAllocationVersion,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken)
        {
            bool matches = allocationPresent
                ? this.Allocation is not null &&
                  this.Allocation.PropertyId == propertyId &&
                  this.Allocation.Id == allocationId &&
                  this.Allocation.MatchesAnonymisedState(
                      resultingAllocationVersion,
                      reservationPseudonym,
                      completedAtUtc)
                : this.Allocation is null;
            return Task.FromResult(matches);
        }

        public Task AddRestoreProofAsync(
            InventoryAllocationAnonymisationRestoreReceipt receipt,
            InventoryAllocationAnonymisationTombstone? newTombstone,
            CancellationToken cancellationToken)
        {
            this.RestoreReceipt = receipt;
            this.Tombstone = newTombstone ?? this.Tombstone;
            this.AddCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOperationLock
        : IInventoryAllocationOperationLock
    {
        public List<Guid> AllocationIds { get; } = [];

        public Task AcquireAsync(
            string tenantId,
            Guid allocationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            this.AllocationIds.Add(allocationId);
            return Task.CompletedTask;
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
