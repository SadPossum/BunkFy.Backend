namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryAllocationRequestedHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 7, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Semantic_duplicate_is_locked_and_does_not_amplify_outbox()
    {
        InventoryAllocation allocation = CreateAllocation();
        List<string> calls = [];
        RecordingOutbox outbox = new();
        RecordingAllocationRepository repository = new(allocation, calls);
        InventoryAllocationRequestedHandler handler = CreateHandler(
            repository,
            calls,
            outbox);

        await handler.HandleAsync(
            CreateRequest(allocation),
            CancellationToken.None);

        Assert.Empty(outbox.Events);
        Assert.Equal(
            ["request-lock", "request-read", "allocation-lock", "allocation-read"],
            calls);
    }

    [Fact]
    public async Task Anonymised_duplicate_is_silently_consumed()
    {
        InventoryAllocation allocation = CreateAllocation();
        InventoryAllocationRequestedIntegrationEvent request =
            CreateRequest(allocation);
        Assert.True(allocation.Release(
            Guid.NewGuid(),
            allocation.Version,
            Now).IsSuccess);
        Assert.True(allocation.Anonymise(
            allocation.Version,
            Guid.NewGuid(),
            Now).IsSuccess);
        RecordingOutbox outbox = new();
        InventoryAllocationRequestedHandler handler = CreateHandler(
            new RecordingAllocationRepository(allocation, []),
            [],
            outbox);

        await handler.HandleAsync(
            request,
            CancellationToken.None);

        Assert.Empty(outbox.Events);
    }

    [Fact]
    public async Task Different_request_for_existing_reservation_is_rejected_after_request_lock()
    {
        InventoryAllocation allocation = CreateAllocation();
        List<string> calls = [];
        RecordingOutbox outbox = new();
        RecordingAllocationRepository repository = new(
            allocation,
            calls,
            matchRequest: false);
        InventoryAllocationRequestedIntegrationEvent request =
            CreateRequest(allocation, Guid.NewGuid());

        await CreateHandler(repository, calls, outbox).HandleAsync(
            request,
            CancellationToken.None);

        InventoryAllocationRejectedIntegrationEvent rejected =
            Assert.IsType<InventoryAllocationRejectedIntegrationEvent>(
                Assert.Single(outbox.Events));
        Assert.Equal(
            InventoryAllocationRejectionReason.ExistingActiveAllocation,
            rejected.Reason);
        Assert.Equal(
            ["request-lock", "request-read", "reservation-read"],
            calls);
    }

    private static InventoryAllocationRequestedHandler CreateHandler(
        RecordingAllocationRepository repository,
        List<string> calls,
        RecordingOutbox outbox)
    {
        RecordingOperationLock operationLock = new(calls);
        return new(
            repository,
            new RecordingRequestLock(calls),
            new InventoryAllocationMutationCoordinator(
                operationLock,
                new TestScopeContext()),
            new ThrowingAvailabilityRepository(),
            new RecordingOutboxRegistry(outbox),
            new TestClock(),
            new TestIdGenerator());
    }

    private static InventoryAllocation CreateAllocation() =>
        InventoryAllocation.CreateAccepted(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 12),
            [Guid.NewGuid()],
            Now).Value;

    private static InventoryAllocationRequestedIntegrationEvent CreateRequest(
        InventoryAllocation allocation,
        Guid? allocationRequestId = null) => new(
        Guid.NewGuid(),
        allocation.ScopeId,
        Now,
        allocation.ReservationId,
        allocationRequestId ?? allocation.AllocationRequestId,
        allocation.PropertyId,
        allocation.Arrival,
        allocation.Departure,
        allocation.Units.Select(unit => unit.InventoryUnitId).ToArray());

    private sealed class RecordingAllocationRepository(
        InventoryAllocation allocation,
        List<string> calls,
        bool matchRequest = true)
        : IInventoryAllocationRepository
    {
        public Task<InventoryAllocation?> GetByRequestAsync(
            Guid allocationRequestId,
            CancellationToken cancellationToken)
        {
            calls.Add("request-read");
            return Task.FromResult<InventoryAllocation?>(
                matchRequest &&
                allocation.AllocationRequestId == allocationRequestId
                    ? allocation
                    : null);
        }

        public Task<InventoryAllocation?> GetByReservationAsync(
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            calls.Add("reservation-read");
            return Task.FromResult<InventoryAllocation?>(
                allocation.ReservationId == reservationId
                    ? allocation
                    : null);
        }

        public Task<InventoryAllocation?> GetAsync(
            Guid allocationId,
            CancellationToken cancellationToken)
        {
            calls.Add("allocation-read");
            return Task.FromResult<InventoryAllocation?>(
                allocation.Id == allocationId ? allocation : null);
        }

        public Task AddAsync(
            InventoryAllocation value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRequestLock(List<string> calls)
        : IInventoryAllocationRequestLock
    {
        public Task AcquireAsync(
            string tenantId,
            Guid allocationRequestId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            calls.Add("request-lock");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOperationLock(List<string> calls)
        : IInventoryAllocationOperationLock
    {
        public Task<bool> TryAcquireExistingAsync(
            string tenantId,
            Guid allocationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            calls.Add("allocation-lock");
            return Task.FromResult(true);
        }

        public Task AcquireCoordinateAsync(
            string tenantId,
            Guid allocationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingAvailabilityRepository
        : IInventoryAvailabilityRepository
    {
        public Task<InventoryAvailabilityContextSnapshot> GetContextAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<InventoryAvailabilityConflictSnapshot> GetConflictsAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> conflictUnitIds,
            DateOnly arrival,
            DateOnly departure,
            Guid? excludedAllocationId,
            IReadOnlyCollection<Guid> excludedBlockIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RoomInventoryImpactSnapshot?> GetRoomImpactAsync(
            Guid propertyId,
            Guid roomId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BedRetirementImpactSnapshot?> GetBedRetirementImpactAsync(
            Guid propertyId,
            Guid roomId,
            Guid bedId,
            Guid? excludedAllocationId,
            IReadOnlyCollection<Guid> excludedBlockIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task TouchUnitsAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => InventoryModuleMetadata.Name;
        public List<IIntegrationEvent> Events { get; } = [];

        public Task EnqueueAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRegistry(RecordingOutbox outbox)
        : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName) => outbox;
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
        public Guid NewId() => Guid.NewGuid();
    }
}
