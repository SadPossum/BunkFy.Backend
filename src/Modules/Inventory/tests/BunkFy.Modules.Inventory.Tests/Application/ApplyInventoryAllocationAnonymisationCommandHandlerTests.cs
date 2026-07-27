namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.Inventory.Application;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    ApplyInventoryAllocationAnonymisationCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Approved_terminal_allocation_executes_once_and_replays()
    {
        InventoryAllocation allocation = CreateReleased();
        RecordingRepository repository = new(allocation);
        RecordingOperationLock operationLock = new();
        RecordingApprovalGate approval = new();
        ApplyInventoryAllocationAnonymisationCommandHandler handler =
            new(
                repository,
                operationLock,
                approval,
                new TestScopeContext(),
                new TestClock(),
                new TestIdGenerator());
        ApplyInventoryAllocationAnonymisationCommand command =
            new(CreateRequest(allocation));

        Result<InventoryAllocationAnonymisationReceiptDto> first =
            await handler.HandleAsync(
                command,
                CancellationToken.None);
        Result<InventoryAllocationAnonymisationReceiptDto> replay =
            await handler.HandleAsync(
                command,
                CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.True(allocation.IsAnonymised);
        Assert.Equal(
            command.Request.Coordinate.RecordVersion + 1,
            first.Value.ResultingAllocationVersion);
        Assert.Equal(2, first.Value.RemovedAmendmentDecisionCount);
        Assert.Single(operationLock.AllocationIds);
        Assert.Equal(1, approval.CallCount);
        Assert.Equal(1, repository.AddCount);
        Assert.NotNull(repository.Tombstone);
        Assert.True(repository.Tombstone.Matches(
            repository.Receipt!));
    }

    [Fact]
    public async Task Changed_retry_conflicts_and_active_allocation_blocks()
    {
        InventoryAllocation allocation = CreateReleased();
        RecordingRepository repository = new(allocation);
        ApplyInventoryAllocationAnonymisationCommandHandler handler =
            CreateHandler(repository);
        DataRightsAnonymisationContributionRequest request =
            CreateRequest(allocation);
        Assert.True((await handler.HandleAsync(
            new(request),
            CancellationToken.None)).IsSuccess);

        Result<InventoryAllocationAnonymisationReceiptDto> changed =
            await handler.HandleAsync(
                new(request with
                {
                    OperationRevision =
                        request.OperationRevision + 1
                }),
                CancellationToken.None);
        Assert.Equal(
            InventoryApplicationErrors
                .AnonymisationIdempotencyConflict,
            changed.Error);

        InventoryAllocation active = CreateActive();
        Result<InventoryAllocationAnonymisationReceiptDto> blocked =
            await CreateHandler(new(active)).HandleAsync(
                new(CreateRequest(active)),
                CancellationToken.None);
        Assert.Equal(
            InventoryApplicationErrors.AnonymisationBlocked(
                ApplyInventoryAllocationAnonymisationCommandHandler
                    .ActiveBlocker),
            blocked.Error);
        Assert.False(active.IsAnonymised);
    }

    private static
        ApplyInventoryAllocationAnonymisationCommandHandler
        CreateHandler(RecordingRepository repository) =>
        new(
            repository,
            new RecordingOperationLock(),
            new RecordingApprovalGate(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

    private static DataRightsAnonymisationContributionRequest
        CreateRequest(InventoryAllocation allocation)
    {
        DataRightsApprovalEvidence evidence = new(
            SchemaVersion: 1,
            allocation.PropertyId,
            PropertyVersion: 7,
            OperatingCountryCode: "GB",
            PolicyId: "gb-hostel",
            PolicyVersion: 3,
            RetentionPolicyId: "inventory-operational",
            RetentionPolicyVersion: 2,
            ContentSha256: new string('a', 64),
            PurposeCode: "data-rights-anonymisation",
            Surface: "erasure",
            SourceProvenance: "authorized-workspace-operator",
            EvaluatedAtUtc: Now.AddMinutes(-5),
            RequiresDistinctExecutor: true);
        return new(
            DataRightsAnonymisationContract.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            allocation.PropertyId,
            Guid.NewGuid(),
            ApprovalRevision: 4,
            OperationRevision: 5,
            new(
                InventoryDataRightsCoordinates.Owner,
                InventoryDataRightsCoordinates.AllocationRecordType,
                allocation.Id,
                allocation.Version),
            evidence,
            "user:privacy-executor",
            Now.AddMinutes(10));
    }

    private static InventoryAllocation CreateActive() =>
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

    private static InventoryAllocation CreateReleased()
    {
        InventoryAllocation allocation = CreateActive();
        Assert.True(allocation.Release(
            Guid.NewGuid(),
            allocation.Version,
            Now.AddHours(-1)).IsSuccess);
        return allocation;
    }

    private sealed class RecordingRepository(
        InventoryAllocation allocation)
        : IInventoryAllocationAnonymisationRepository
    {
        public InventoryAllocationAnonymisationReceipt? Receipt
        {
            get;
            private set;
        }

        public InventoryAllocationAnonymisationTombstone? Tombstone
        {
            get;
            private set;
        }

        public int AddCount { get; private set; }

        public Task<InventoryAllocationAnonymisationReceipt?>
            FindReceiptByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Receipt?.IdempotencyKey == idempotencyKey
                    ? this.Receipt
                    : null);

        public Task<InventoryAllocation?> GetAllocationAsync(
            Guid propertyId,
            Guid allocationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<InventoryAllocation?>(
                allocation.PropertyId == propertyId &&
                allocation.Id == allocationId
                    ? allocation
                    : null);

        public Task<int> RemoveAmendmentDecisionsAsync(
            Guid propertyId,
            Guid allocationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(2);

        public Task<bool> VerifyOwnerStateAsync(
            InventoryAllocationAnonymisationReceipt receipt,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Receipt == receipt &&
                this.Tombstone?.Matches(receipt) == true);

        public Task AddOwnerProofAsync(
            InventoryAllocationAnonymisationReceipt receipt,
            InventoryAllocationAnonymisationTombstone tombstone,
            CancellationToken cancellationToken)
        {
            this.Receipt = receipt;
            this.Tombstone = tombstone;
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

    private sealed class RecordingApprovalGate
        : IDataRightsOperationApprovalGate
    {
        public int CallCount { get; private set; }

        public Task<DataRightsOperationApprovalResult> EvaluateAsync(
            DataRightsOperationApprovalRequest request,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            DataRightsApprovalEvidence evidence = new(
                SchemaVersion: 1,
                request.PropertyId,
                PropertyVersion: 7,
                OperatingCountryCode: "GB",
                PolicyId: "gb-hostel",
                PolicyVersion: 3,
                RetentionPolicyId: "inventory-operational",
                RetentionPolicyVersion: 2,
                ContentSha256: new string('a', 64),
                PurposeCode: "data-rights-anonymisation",
                Surface: "erasure",
                SourceProvenance:
                    "authorized-workspace-operator",
                EvaluatedAtUtc: Now.AddMinutes(-5),
                RequiresDistinctExecutor: true);
            return Task.FromResult(
                DataRightsOperationApprovalResult
                    .ApprovedWithEvidence(evidence));
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

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
