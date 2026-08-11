namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.Inventory.Application;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryRetirementCommandHandlerTests
{
    private const string TenantId = "tenant-a";
    private static readonly Guid PropertyId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid RoomId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid BedId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(
        2026,
        8,
        9,
        2,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task Bed_request_exact_replay_returns_the_fresh_process_view()
    {
        BedRetirementProcess process = CreateBedProcess();
        Guid operationId = Guid.NewGuid();
        string fingerprint = InventoryManagementMutationFingerprint
            .ComputeBedRetirementRequest(
                PropertyId,
                RoomId,
                BedId,
                "Maintenance");
        RecordingOperationRepository operations = new();
        operations.Seed(InventoryManagementOperationRecord.ForRetirement(
            operationId,
            TenantId,
            PropertyId,
            InventoryManagementResourceKind.InventoryUnit,
            BedId,
            InventoryManagementMutationKind.BedRetirementRequest,
            expectedVersion: 0,
            fingerprint,
            process.Id,
            process.Version,
            Now));
        Assert.True(process.RequestFinalization(Guid.NewGuid(), Now).IsSuccess);
        Harness harness = CreateHarness(process, roomProcess: null, operations);

        Result<BedRetirementDto> result = await harness.BedRequest.HandleAsync(
            new(
                operationId,
                PropertyId,
                RoomId,
                BedId,
                "  Maintenance  ",
                "user:other"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(process.Id, result.Value.TopologyChangeId);
        Assert.Equal(InventoryRetirementStatus.FinalizationRequested, result.Value.Status);
        Assert.Equal(process.Version, result.Value.Version);
        Assert.Empty(operations.Added);
        Assert.Equal(["Room"], harness.Lock.ResourceKinds);
        Assert.Equal(0, harness.Ids.CallCount);
    }

    [Fact]
    public async Task Bed_request_adopts_same_reason_but_rejects_changed_intent()
    {
        BedRetirementProcess process = CreateBedProcess();
        RecordingOperationRepository operations = new();
        Harness harness = CreateHarness(process, roomProcess: null, operations);

        Result<BedRetirementDto> adopted = await harness.BedRequest.HandleAsync(
            new(
                Guid.NewGuid(),
                PropertyId,
                RoomId,
                BedId,
                " Maintenance ",
                "user:other"),
            CancellationToken.None);
        Result<BedRetirementDto> changed = await harness.BedRequest.HandleAsync(
            new(
                Guid.NewGuid(),
                PropertyId,
                RoomId,
                BedId,
                "Replace the frame",
                "user:other"),
            CancellationToken.None);

        Assert.True(adopted.IsSuccess);
        Assert.Equal(
            InventoryApplicationErrors.RetirementRequestConflict,
            changed.Error);
        InventoryManagementOperationRecord stored = Assert.Single(
            operations.Added);
        Assert.Equal(
            InventoryManagementMutationKind.BedRetirementRequest,
            stored.Kind);
        Assert.Equal(process.Id, stored.ResultTopologyChangeId);
        Assert.Equal(process.Version, stored.ResultVersion);
        Assert.Equal(0, harness.Ids.CallCount);
    }

    [Fact]
    public async Task Bed_request_does_not_adopt_a_process_from_another_room_coordinate()
    {
        BedRetirementProcess process = CreateBedProcess();
        RecordingOperationRepository operations = new();
        Harness harness = CreateHarness(process, roomProcess: null, operations);

        Result<BedRetirementDto> result = await harness.BedRequest.HandleAsync(
            new(
                Guid.NewGuid(),
                PropertyId,
                Guid.NewGuid(),
                BedId,
                "Maintenance",
                "user:other"),
            CancellationToken.None);

        Assert.Equal(InventoryApplicationErrors.InventoryUnitNotFound, result.Error);
        Assert.Empty(operations.Added);
    }

    [Fact]
    public async Task Bed_retry_is_versioned_and_exact_replay_survives_advancement()
    {
        BedRetirementProcess process = CreateRejectedBedProcess();
        long rejectedVersion = process.Version;
        Guid operationId = Guid.NewGuid();
        RecordingOperationRepository operations = new();
        Harness harness = CreateHarness(process, roomProcess: null, operations);

        Result<BedRetirementDto> retried = await harness.BedRetry.HandleAsync(
            new(operationId, PropertyId, process.Id, rejectedVersion),
            CancellationToken.None);
        int generatedBeforeReplay = harness.Ids.CallCount;
        Assert.True(process.MarkFinalized(Now.AddMinutes(1)).IsSuccess);
        Result<BedRetirementDto> replayed = await harness.BedRetry.HandleAsync(
            new(operationId, PropertyId, process.Id, rejectedVersion),
            CancellationToken.None);

        Assert.True(retried.IsSuccess);
        Assert.Equal(
            InventoryRetirementStatus.FinalizationRequested,
            retried.Value.Status);
        Assert.True(replayed.IsSuccess);
        Assert.Equal(
            InventoryRetirementStatus.FinalizedAwaitingTopology,
            replayed.Value.Status);
        Assert.Single(operations.Added);
        Assert.Equal(generatedBeforeReplay, harness.Ids.CallCount);
        Assert.Equal(
            ["BedRetirement", "Room", "BedRetirement"],
            harness.Lock.ResourceKinds);
    }

    [Fact]
    public async Task Bed_retry_stale_version_does_not_bind_the_operation()
    {
        BedRetirementProcess process = CreateRejectedBedProcess();
        RecordingOperationRepository operations = new();
        Harness harness = CreateHarness(process, roomProcess: null, operations);

        Result<BedRetirementDto> result = await harness.BedRetry.HandleAsync(
            new(Guid.NewGuid(), PropertyId, process.Id, process.Version - 1),
            CancellationToken.None);

        Assert.Equal(InventoryApplicationErrors.VersionConflict, result.Error);
        Assert.Empty(operations.Added);
        Assert.Equal(0, harness.Ids.CallCount);
        Assert.Equal(
            ["BedRetirement", "Room"],
            harness.Lock.ResourceKinds);
    }

    [Fact]
    public async Task Room_request_adopts_same_reason_and_journals_a_pointer()
    {
        RoomRetirementProcess process = CreateRoomProcess();
        RecordingOperationRepository operations = new();
        Harness harness = CreateHarness(bedProcess: null, process, operations);

        Result<RoomRetirementDto> result = await harness.RoomRequest.HandleAsync(
            new(
                Guid.NewGuid(),
                PropertyId,
                RoomId,
                "  Repurpose room  ",
                "user:other"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        InventoryManagementOperationRecord stored = Assert.Single(
            operations.Added);
        Assert.Equal(
            InventoryManagementMutationKind.RoomRetirementRequest,
            stored.Kind);
        Assert.Equal(process.Id, stored.ResultTopologyChangeId);
        Assert.Equal(["Room"], harness.Lock.ResourceKinds);
    }

    [Fact]
    public async Task Room_retry_records_expected_and_result_versions()
    {
        RoomRetirementProcess process = CreateRejectedRoomProcess();
        long rejectedVersion = process.Version;
        RecordingOperationRepository operations = new();
        Harness harness = CreateHarness(bedProcess: null, process, operations);

        Result<RoomRetirementDto> result = await harness.RoomRetry.HandleAsync(
            new(Guid.NewGuid(), PropertyId, process.Id, rejectedVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        InventoryManagementOperationRecord stored = Assert.Single(
            operations.Added);
        Assert.Equal(
            InventoryManagementMutationKind.RoomRetirementRetry,
            stored.Kind);
        Assert.Equal(rejectedVersion, stored.ExpectedVersion);
        Assert.Equal(rejectedVersion + 1, stored.ResultVersion);
        Assert.Equal(process.Id, stored.ResultTopologyChangeId);
        Assert.Equal(
            ["RoomRetirement", "Room"],
            harness.Lock.ResourceKinds);
    }

    private static Harness CreateHarness(
        BedRetirementProcess? bedProcess,
        RoomRetirementProcess? roomProcess,
        RecordingOperationRepository operations)
    {
        TestScopeContext scope = new();
        RecordingManagementLock operationLock = new();
        InventoryManagementMutationCoordinator mutations = new(
            operationLock,
            scope);
        InventoryManagementOperationJournal journal = new(operations);
        FakeBedRetirementRepository beds = new(bedProcess);
        FakeRoomRetirementRepository rooms = new(roomProcess);
        FakeAvailabilityRepository availability = new();
        TestClock clock = new();
        TestIdGenerator ids = new();
        BedRetirementCoordinator bedCoordinator = new(
            beds,
            availability,
            clock,
            ids);
        RoomRetirementCoordinator roomCoordinator = new(
            rooms,
            availability,
            clock,
            ids);
        return new(
            new(
                mutations,
                journal,
                null!,
                beds,
                rooms,
                availability,
                bedCoordinator,
                null!,
                scope,
                clock,
                ids),
            new(
                mutations,
                journal,
                beds,
                availability,
                bedCoordinator,
                clock,
                ids),
            new(
                mutations,
                journal,
                null!,
                null!,
                rooms,
                availability,
                roomCoordinator,
                null!,
                scope,
                clock,
                ids),
            new(
                mutations,
                journal,
                rooms,
                availability,
                roomCoordinator,
                clock,
                ids),
            operationLock,
            ids);
    }

    private static BedRetirementProcess CreateBedProcess() =>
        BedRetirementProcess.Create(
            Guid.NewGuid(),
            TenantId,
            PropertyId,
            RoomId,
            BedId,
            "Maintenance",
            "user:operator",
            Now).Value;

    private static BedRetirementProcess CreateRejectedBedProcess()
    {
        BedRetirementProcess process = CreateBedProcess();
        Assert.True(process.RequestFinalization(Guid.NewGuid(), Now).IsSuccess);
        Assert.True(process.Reject(1, Now).IsSuccess);
        return process;
    }

    private static RoomRetirementProcess CreateRoomProcess() =>
        RoomRetirementProcess.Create(
            Guid.NewGuid(),
            TenantId,
            PropertyId,
            RoomId,
            "Repurpose room",
            "user:operator",
            Now).Value;

    private static RoomRetirementProcess CreateRejectedRoomProcess()
    {
        RoomRetirementProcess process = CreateRoomProcess();
        Assert.True(process.RequestFinalization(Guid.NewGuid(), Now).IsSuccess);
        Assert.True(process.Reject(1, Now).IsSuccess);
        return process;
    }

    private sealed record Harness(
        RequestBedRetirementCommandHandler BedRequest,
        RetryBedRetirementCommandHandler BedRetry,
        RequestRoomRetirementCommandHandler RoomRequest,
        RetryRoomRetirementCommandHandler RoomRetry,
        RecordingManagementLock Lock,
        TestIdGenerator Ids);

    private sealed class RecordingOperationRepository
        : IInventoryManagementOperationRepository
    {
        private readonly Dictionary<
            (InventoryManagementResourceKind, Guid, Guid),
            InventoryManagementOperationRecord> operations = [];

        public List<InventoryManagementOperationRecord> Added { get; } = [];

        public void Seed(InventoryManagementOperationRecord operation) =>
            this.operations.Add(
                (operation.ResourceKind,
                 operation.ResourceId,
                 operation.OperationId),
                operation);

        public Task<InventoryManagementOperationRecord?> GetAsync(
            InventoryManagementResourceKind resourceKind,
            Guid resourceId,
            Guid operationId,
            CancellationToken cancellationToken)
        {
            this.operations.TryGetValue(
                (resourceKind, resourceId, operationId),
                out InventoryManagementOperationRecord? operation);
            return Task.FromResult(operation);
        }

        public Task AddAsync(
            InventoryManagementOperationRecord operation,
            CancellationToken cancellationToken)
        {
            this.operations.Add(
                (operation.ResourceKind,
                 operation.ResourceId,
                 operation.OperationId),
                operation);
            this.Added.Add(operation);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingManagementLock : IInventoryManagementLock
    {
        public List<string> ResourceKinds { get; } = [];

        public Task AcquireResourceAsync(
            string tenantId,
            InventoryManagementResourceKind resourceKind,
            Guid resourceId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(TenantId, tenantId);
            Assert.NotEqual(Guid.Empty, resourceId);
            this.ResourceKinds.Add(resourceKind.ToString());
            return Task.CompletedTask;
        }

        public Task AcquireOperationAsync(
            string tenantId,
            InventoryManagementResourceKind resourceKind,
            Guid resourceId,
            Guid operationId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "Retirement paths use resource locks.");
    }

    private sealed class FakeBedRetirementRepository(
        BedRetirementProcess? process) : IBedRetirementRepository
    {
        public Task<BedRetirementProcess?> GetAsync(
            Guid propertyId,
            Guid topologyChangeId,
            CancellationToken cancellationToken) => Task.FromResult(
                process is not null &&
                process.PropertyId == propertyId &&
                process.Id == topologyChangeId
                    ? process
                    : null);

        public Task<Guid?> GetTopologyChangeIdByBedAsync(
            Guid propertyId,
            Guid bedId,
            CancellationToken cancellationToken) => Task.FromResult<Guid?>(
                process is not null &&
                process.PropertyId == propertyId &&
                process.BedId == bedId
                    ? process.Id
                    : null);

        public Task<BedRetirementProcess?> GetByBedAsync(
            Guid propertyId,
            Guid bedId,
            CancellationToken cancellationToken) => Task.FromResult(
                process is not null &&
                process.PropertyId == propertyId &&
                process.BedId == bedId
                    ? process
                    : null);

        public Task<IReadOnlyCollection<BedRetirementProcess>>
            ListActiveForUnitsAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) => Task.FromResult<
                IReadOnlyCollection<BedRetirementProcess>>(
                    process is null ? [] : [process]);

        public Task AddAsync(
            BedRetirementProcess value,
            CancellationToken cancellationToken) => throw new
                InvalidOperationException("Creation is outside this harness.");
    }

    private sealed class FakeRoomRetirementRepository(
        RoomRetirementProcess? process) : IRoomRetirementRepository
    {
        public Task<RoomRetirementProcess?> GetAsync(
            Guid propertyId,
            Guid topologyChangeId,
            CancellationToken cancellationToken) => Task.FromResult(
                process is not null &&
                process.PropertyId == propertyId &&
                process.Id == topologyChangeId
                    ? process
                    : null);

        public Task<Guid?> GetTopologyChangeIdByRoomAsync(
            Guid propertyId,
            Guid roomId,
            CancellationToken cancellationToken) => Task.FromResult<Guid?>(
                process is not null &&
                process.PropertyId == propertyId &&
                process.RoomId == roomId
                    ? process.Id
                    : null);

        public Task<RoomRetirementProcess?> GetByRoomAsync(
            Guid propertyId,
            Guid roomId,
            CancellationToken cancellationToken) => Task.FromResult(
                process is not null &&
                process.PropertyId == propertyId &&
                process.RoomId == roomId
                    ? process
                    : null);

        public Task<IReadOnlyCollection<RoomRetirementProcess>>
            ListActiveForUnitsAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) => Task.FromResult<
                IReadOnlyCollection<RoomRetirementProcess>>(
                    process is null ? [] : [process]);

        public Task AddAsync(
            RoomRetirementProcess value,
            CancellationToken cancellationToken) => throw new
                InvalidOperationException("Creation is outside this harness.");
    }

    private sealed class FakeAvailabilityRepository
        : IInventoryAvailabilityRepository
    {
        public Task<InventoryAvailabilityContextSnapshot> GetContextAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not used by retirement tests.");

        public Task<InventoryAvailabilityConflictSnapshot> GetConflictsAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> conflictUnitIds,
            DateOnly arrival,
            DateOnly departure,
            Guid? excludedAllocationId,
            IReadOnlyCollection<Guid> excludedBlockIds,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not used by retirement tests.");

        public Task<RoomInventoryImpactSnapshot?> GetRoomImpactAsync(
            Guid propertyId,
            Guid roomId,
            CancellationToken cancellationToken) => Task.FromResult<
                RoomInventoryImpactSnapshot?>(
                    propertyId == PropertyId && roomId == RoomId
                        ? new(0, 0, 0, 1, [], false)
                        : null);

        public Task<BedRetirementImpactSnapshot?>
            GetBedRetirementImpactAsync(
            Guid propertyId,
            Guid roomId,
            Guid bedId,
            Guid? excludedAllocationId,
            IReadOnlyCollection<Guid> excludedBlockIds,
            CancellationToken cancellationToken) => Task.FromResult<
                BedRetirementImpactSnapshot?>(
                    propertyId == PropertyId &&
                    roomId == RoomId &&
                    bedId == BedId
                        ? new(0, 0, [], false)
                        : null);

        public Task TouchUnitsAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not used by retirement tests.");
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public int CallCount { get; private set; }

        public Guid NewId()
        {
            this.CallCount++;
            return Guid.CreateVersion7();
        }
    }
}
