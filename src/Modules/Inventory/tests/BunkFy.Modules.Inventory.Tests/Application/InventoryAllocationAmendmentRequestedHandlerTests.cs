namespace BunkFy.Modules.Inventory.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryAllocationAmendmentRequestedHandlerTests
{
    private const string ScopeId = "tenant-a";
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PropertyId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ReservationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid AllocationId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid UnitId = Guid.Parse("40000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Exact_replay_republishes_the_decision_without_mutating_twice()
    {
        InventoryAllocation allocation = InventoryAllocation.CreateAccepted(
            AllocationId,
            ScopeId,
            ReservationId,
            Guid.NewGuid(),
            PropertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [UnitId],
            Now).Value;
        FakeAllocationRepository allocations = new(allocation);
        FakeDecisionRepository decisions = new();
        RecordingOutbox outbox = new();
        InventoryAllocationAmendmentRequestedHandler handler = new(
            allocations,
            decisions,
            new RecordingOutboxRegistry(outbox),
            new TestClock(),
            new TestIdGenerator());
        Guid amendmentId = Guid.NewGuid();
        InventoryAllocationAmendmentRequestedIntegrationEvent request = Request(
            amendmentId,
            new DateOnly(2026, 8, 4));

        await handler.HandleAsync(request, CancellationToken.None);
        await handler.HandleAsync(request, CancellationToken.None);
        await handler.HandleAsync(Request(amendmentId, new DateOnly(2026, 8, 5)), CancellationToken.None);

        Assert.Equal(2, allocation.Version);
        Assert.Equal(new DateOnly(2026, 8, 4), allocation.Departure);
        Assert.Equal(1, allocations.TouchCount);
        InventoryAllocationAmendmentDecisionRecord decision = Assert.Single(decisions.Items.Values);
        Assert.True(decision.Confirmed);
        Assert.Equal(2, decision.AllocationVersion);
        Assert.Collection(
            outbox.Events,
            item => Assert.Equal(2, Assert.IsType<InventoryAllocationAmendmentConfirmedIntegrationEvent>(item).AllocationVersion),
            item => Assert.Equal(2, Assert.IsType<InventoryAllocationAmendmentConfirmedIntegrationEvent>(item).AllocationVersion),
            item => Assert.Equal(
                InventoryAllocationRejectionReason.RequestMismatch,
                Assert.IsType<InventoryAllocationAmendmentRejectedIntegrationEvent>(item).Reason));
    }

    private static InventoryAllocationAmendmentRequestedIntegrationEvent Request(
        Guid amendmentId,
        DateOnly departure) => new(
        Guid.NewGuid(),
        ScopeId,
        Now,
        amendmentId,
        AllocationId,
        ReservationId,
        PropertyId,
        expectedAllocationVersion: 1,
        new DateOnly(2026, 8, 1),
        departure,
        [UnitId]);

    private sealed class FakeAllocationRepository(InventoryAllocation allocation) : IInventoryAllocationRepository
    {
        public int TouchCount { get; private set; }

        public Task<InventoryAllocation?> GetByRequestAsync(
            Guid allocationRequestId,
            CancellationToken cancellationToken) => Task.FromResult<InventoryAllocation?>(null);

        public Task<InventoryAllocation?> GetByReservationAsync(
            Guid reservationId,
            CancellationToken cancellationToken) => Task.FromResult<InventoryAllocation?>(allocation);

        public Task<InventoryAllocation?> GetAsync(
            Guid allocationId,
            CancellationToken cancellationToken) => Task.FromResult<InventoryAllocation?>(
            allocation.Id == allocationId ? allocation : null);

        public Task AddAsync(InventoryAllocation value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<InventoryAllocationUnitSnapshot>> GetUnitsAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<InventoryAllocationUnitSnapshot>>(
            inventoryUnitIds.Select(id => new InventoryAllocationUnitSnapshot(id, true, true)).ToArray());

        public Task<bool> HasManualBlockConflictAsync(
            IReadOnlyCollection<Guid> inventoryUnitIds,
            DateOnly arrival,
            DateOnly departure,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> HasActiveAllocationConflictAsync(
            IReadOnlyCollection<Guid> inventoryUnitIds,
            DateOnly arrival,
            DateOnly departure,
            Guid? excludedAllocationId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task TouchUnitsAsync(
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken)
        {
            this.TouchCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDecisionRepository : IInventoryAllocationAmendmentDecisionRepository
    {
        public Dictionary<Guid, InventoryAllocationAmendmentDecisionRecord> Items { get; } = [];

        public Task<InventoryAllocationAmendmentDecisionRecord?> GetAsync(
            Guid amendmentRequestId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.GetValueOrDefault(amendmentRequestId));

        public Task AddAsync(
            InventoryAllocationAmendmentDecisionRecord decision,
            CancellationToken cancellationToken)
        {
            this.Items.Add(decision.AmendmentRequestId, decision);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => InventoryModuleMetadata.Name;
        public List<IIntegrationEvent> Events { get; } = [];

        public Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRegistry(RecordingOutbox outbox) : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName) => outbox;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
