namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryRetirementOutcomeCoordinatorTests
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
        11,
        4,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task Room_topology_then_finalization_converges_under_the_process_lock()
    {
        RoomRetirementProcess process = CreateRequestedRoomProcess();
        Harness harness = CreateHarness(null, process);

        await harness.Room.CompleteFromTopologyAsync(
            PropertyId,
            RoomId,
            CancellationToken.None);
        await harness.Room.MarkFinalizedAsync(
            PropertyId,
            process.Id,
            RoomId,
            CancellationToken.None);

        Assert.Equal(InventoryRetirementProcessState.Completed, process.State);
        Assert.Equal(
            ["identity:room", "lock:RoomRetirement", "load:room", "lock:RoomRetirement", "load:room"],
            harness.Calls);
    }

    [Fact]
    public async Task Room_finalization_then_topology_converges_under_the_process_lock()
    {
        RoomRetirementProcess process = CreateRequestedRoomProcess();
        Harness harness = CreateHarness(null, process);

        await harness.Room.MarkFinalizedAsync(
            PropertyId,
            process.Id,
            RoomId,
            CancellationToken.None);
        await harness.Room.CompleteFromTopologyAsync(
            PropertyId,
            RoomId,
            CancellationToken.None);

        Assert.Equal(InventoryRetirementProcessState.Completed, process.State);
        Assert.Equal(
            ["lock:RoomRetirement", "load:room", "identity:room", "lock:RoomRetirement", "load:room"],
            harness.Calls);
    }

    [Fact]
    public async Task Bed_topology_then_finalization_converges_under_the_process_lock()
    {
        BedRetirementProcess process = CreateRequestedBedProcess();
        Harness harness = CreateHarness(process, null);

        await harness.Bed.CompleteFromTopologyAsync(
            PropertyId,
            RoomId,
            BedId,
            CancellationToken.None);
        await harness.Bed.MarkFinalizedAsync(
            PropertyId,
            process.Id,
            RoomId,
            BedId,
            CancellationToken.None);

        Assert.Equal(InventoryRetirementProcessState.Completed, process.State);
        Assert.Equal(
            ["identity:bed", "lock:BedRetirement", "load:bed", "lock:BedRetirement", "load:bed"],
            harness.Calls);
    }

    [Fact]
    public async Task Bed_finalization_then_topology_converges_under_the_process_lock()
    {
        BedRetirementProcess process = CreateRequestedBedProcess();
        Harness harness = CreateHarness(process, null);

        await harness.Bed.MarkFinalizedAsync(
            PropertyId,
            process.Id,
            RoomId,
            BedId,
            CancellationToken.None);
        await harness.Bed.CompleteFromTopologyAsync(
            PropertyId,
            RoomId,
            BedId,
            CancellationToken.None);

        Assert.Equal(InventoryRetirementProcessState.Completed, process.State);
        Assert.Equal(
            ["lock:BedRetirement", "load:bed", "identity:bed", "lock:BedRetirement", "load:bed"],
            harness.Calls);
    }

    [Fact]
    public async Task Topology_without_a_retirement_process_does_not_take_a_process_lock()
    {
        Harness harness = CreateHarness(null, null);

        await harness.Room.CompleteFromTopologyAsync(
            PropertyId,
            RoomId,
            CancellationToken.None);
        await harness.Bed.CompleteFromTopologyAsync(
            PropertyId,
            RoomId,
            BedId,
            CancellationToken.None);

        Assert.Equal(["identity:room", "identity:bed"], harness.Calls);
    }

    [Fact]
    public async Task Duplicate_rejections_preserve_the_terminal_reason()
    {
        RoomRetirementProcess room = CreateRequestedRoomProcess();
        BedRetirementProcess bed = CreateRequestedBedProcess();
        Harness harness = CreateHarness(bed, room);

        await harness.Room.RejectAsync(
            PropertyId,
            room.Id,
            RoomId,
            reasonCode: 7,
            CancellationToken.None);
        await harness.Room.RejectAsync(
            PropertyId,
            room.Id,
            RoomId,
            reasonCode: 7,
            CancellationToken.None);
        await harness.Bed.RejectAsync(
            PropertyId,
            bed.Id,
            RoomId,
            BedId,
            reasonCode: 8,
            CancellationToken.None);
        await harness.Bed.RejectAsync(
            PropertyId,
            bed.Id,
            RoomId,
            BedId,
            reasonCode: 8,
            CancellationToken.None);

        Assert.Equal(InventoryRetirementProcessState.Rejected, room.State);
        Assert.Equal(7, room.RejectionReasonCode);
        Assert.Equal(InventoryRetirementProcessState.Rejected, bed.State);
        Assert.Equal(8, bed.RejectionReasonCode);
        Assert.Equal(4, harness.Calls.Count(call => call.StartsWith("lock:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Correlated_outcomes_fail_closed_after_locking_when_coordinates_do_not_match()
    {
        RoomRetirementProcess room = CreateRequestedRoomProcess();
        BedRetirementProcess bed = CreateRequestedBedProcess();
        Harness harness = CreateHarness(bed, room);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Room.MarkFinalizedAsync(
                PropertyId,
                room.Id,
                Guid.NewGuid(),
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Bed.MarkFinalizedAsync(
                PropertyId,
                bed.Id,
                RoomId,
                Guid.NewGuid(),
                CancellationToken.None));

        Assert.Equal(
            ["lock:RoomRetirement", "load:room", "lock:BedRetirement", "load:bed"],
            harness.Calls);
    }

    private static Harness CreateHarness(
        BedRetirementProcess? bed,
        RoomRetirementProcess? room)
    {
        List<string> calls = [];
        RecordingManagementLock operationLock = new(calls);
        InventoryManagementMutationCoordinator mutations = new(
            operationLock,
            new TestScopeContext());
        return new(
            new(
                mutations,
                new FakeBedRetirementRepository(bed, calls),
                new TestClock()),
            new(
                mutations,
                new FakeRoomRetirementRepository(room, calls),
                new TestClock()),
            calls);
    }

    private static RoomRetirementProcess CreateRequestedRoomProcess()
    {
        RoomRetirementProcess process = RoomRetirementProcess.Create(
            Guid.NewGuid(),
            TenantId,
            PropertyId,
            RoomId,
            "Repurpose room",
            "user:operator",
            Now).Value;
        Assert.True(process.RequestFinalization(Guid.NewGuid(), Now).IsSuccess);
        return process;
    }

    private static BedRetirementProcess CreateRequestedBedProcess()
    {
        BedRetirementProcess process = BedRetirementProcess.Create(
            Guid.NewGuid(),
            TenantId,
            PropertyId,
            RoomId,
            BedId,
            "Replace bed",
            "user:operator",
            Now).Value;
        Assert.True(process.RequestFinalization(Guid.NewGuid(), Now).IsSuccess);
        return process;
    }

    private sealed record Harness(
        BedRetirementOutcomeCoordinator Bed,
        RoomRetirementOutcomeCoordinator Room,
        List<string> Calls);

    private sealed class RecordingManagementLock(List<string> calls)
        : IInventoryManagementLock
    {
        public Task AcquireResourceAsync(
            string tenantId,
            InventoryManagementResourceKind resourceKind,
            Guid resourceId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(TenantId, tenantId);
            Assert.NotEqual(Guid.Empty, resourceId);
            calls.Add($"lock:{resourceKind}");
            return Task.CompletedTask;
        }

        public Task AcquireOperationAsync(
            string tenantId,
            InventoryManagementResourceKind resourceKind,
            Guid resourceId,
            Guid operationId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Retirement outcomes use process locks.");
    }

    private sealed class FakeRoomRetirementRepository(
        RoomRetirementProcess? process,
        List<string> calls) : IRoomRetirementRepository
    {
        public Task<RoomRetirementProcess?> GetAsync(
            Guid propertyId,
            Guid topologyChangeId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("lock:RoomRetirement", calls[^1]);
            calls.Add("load:room");
            return Task.FromResult(
                process is not null &&
                process.PropertyId == propertyId &&
                process.Id == topologyChangeId
                    ? process
                    : null);
        }

        public Task<Guid?> GetTopologyChangeIdByRoomAsync(
            Guid propertyId,
            Guid roomId,
            CancellationToken cancellationToken)
        {
            calls.Add("identity:room");
            return Task.FromResult<Guid?>(
                process is not null &&
                process.PropertyId == propertyId &&
                process.RoomId == roomId
                    ? process.Id
                    : null);
        }

        public Task<RoomRetirementProcess?> GetByRoomAsync(
            Guid propertyId,
            Guid roomId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<RoomRetirementProcess>> ListActiveForUnitsAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(
            RoomRetirementProcess value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeBedRetirementRepository(
        BedRetirementProcess? process,
        List<string> calls) : IBedRetirementRepository
    {
        public Task<BedRetirementProcess?> GetAsync(
            Guid propertyId,
            Guid topologyChangeId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("lock:BedRetirement", calls[^1]);
            calls.Add("load:bed");
            return Task.FromResult(
                process is not null &&
                process.PropertyId == propertyId &&
                process.Id == topologyChangeId
                    ? process
                    : null);
        }

        public Task<Guid?> GetTopologyChangeIdByBedAsync(
            Guid propertyId,
            Guid bedId,
            CancellationToken cancellationToken)
        {
            calls.Add("identity:bed");
            return Task.FromResult<Guid?>(
                process is not null &&
                process.PropertyId == propertyId &&
                process.BedId == bedId
                    ? process.Id
                    : null);
        }

        public Task<BedRetirementProcess?> GetByBedAsync(
            Guid propertyId,
            Guid bedId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<BedRetirementProcess>> ListActiveForUnitsAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(
            BedRetirementProcess value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
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
}
