namespace BunkFy.Modules.Reservations.Tests;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Events;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationStayLifecycleTests
{
    [Fact]
    public async Task Check_in_handler_preserves_business_date_and_actor_provenance()
    {
        Reservation reservation = CreateConfirmedReservation();
        CheckInReservationCommandHandler handler = new(
            new FakeReservationRepository(reservation),
            new TestClock(),
            new TestIdGenerator());

        Result<ReservationDto> result = await handler.HandleAsync(
            new CheckInReservationCommand(
                reservation.PropertyId,
                reservation.Id,
                new DateOnly(2026, 8, 1),
                reservation.Version,
                "  user:operator-a  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(ReservationStatus.CheckedIn, result.Value.Status);
        Assert.Equal(new DateOnly(2026, 8, 1), result.Value.CheckedInBusinessDate);
        Assert.Equal("user:operator-a", result.Value.CheckedInBy);
        Assert.Equal(TestClock.Now, result.Value.CheckedInAtUtc);
    }

    [Fact]
    public async Task Stay_projectors_publish_correlated_release_and_terminal_events()
    {
        RecordingOutbox outbox = new();
        RecordingOutboxRegistry registry = new(outbox);
        Guid reservationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid allocationId = Guid.NewGuid();
        Guid releaseRequestId = Guid.NewGuid();
        DateOnly businessDate = new(2026, 8, 1);

        await new ReservationCheckedInOutboxProjector(registry).HandleAsync(
            new(Guid.NewGuid(), TestClock.Now, "tenant-a", reservationId, propertyId,
                businessDate, "user:operator-a", 3),
            CancellationToken.None);
        await new ReservationNoShowRequestedOutboxProjector(registry).HandleAsync(
            new(Guid.NewGuid(), TestClock.Now, "tenant-a", reservationId, propertyId,
                allocationId, releaseRequestId, 2),
            CancellationToken.None);
        await new ReservationNoShowOutboxProjector(registry).HandleAsync(
            new(Guid.NewGuid(), TestClock.Now, "tenant-a", reservationId, propertyId,
                businessDate, "user:operator-a", 4),
            CancellationToken.None);
        await new ReservationCheckoutRequestedOutboxProjector(registry).HandleAsync(
            new(Guid.NewGuid(), TestClock.Now, "tenant-a", reservationId, propertyId,
                allocationId, releaseRequestId, 2),
            CancellationToken.None);
        await new ReservationCheckedOutOutboxProjector(registry).HandleAsync(
            new(Guid.NewGuid(), TestClock.Now, "tenant-a", reservationId, propertyId,
                businessDate, "user:operator-a", 5),
            CancellationToken.None);

        Assert.Collection(
            outbox.Events,
            item => Assert.IsType<ReservationCheckedInIntegrationEvent>(item),
            item => Assert.Equal(
                releaseRequestId,
                Assert.IsType<InventoryAllocationReleaseRequestedIntegrationEvent>(item).ReleaseRequestId),
            item => Assert.IsType<ReservationNoShowIntegrationEvent>(item),
            item => Assert.Equal(
                releaseRequestId,
                Assert.IsType<InventoryAllocationReleaseRequestedIntegrationEvent>(item).ReleaseRequestId),
            item => Assert.IsType<ReservationCheckedOutIntegrationEvent>(item));
    }

    [Fact]
    public async Task Release_consumer_fences_projection_and_allows_exact_duplicate_repair()
    {
        Reservation reservation = CreateConfirmedReservation();
        RecordingInventoryProjection projection = new();
        InventoryAllocationReleasedHandler handler = new(
            new FakeReservationRepository(reservation),
            projection,
            new ReservationInboxDomainEventDispatcher(new NoOpDomainEventDispatcher()),
            new TestClock(),
            new TestIdGenerator());

        await handler.HandleAsync(
            new(
                Guid.NewGuid(),
                reservation.ScopeId,
                TestClock.Now,
                reservation.AllocationId!.Value,
                reservation.Id,
                Guid.NewGuid(),
                reservation.AllocationVersion!.Value),
            CancellationToken.None);

        Assert.Equal(ReservationState.Confirmed, reservation.Status);
        Assert.Equal(0, projection.ReleaseCount);

        Guid releaseRequestId = Guid.NewGuid();
        Assert.True(reservation.RequestNoShow(
            reservation.Version,
            reservation.Arrival,
            "user:operator-a",
            releaseRequestId,
            Guid.NewGuid(),
            TestClock.Now).IsSuccess);
        InventoryAllocationReleasedIntegrationEvent released = new(
            Guid.NewGuid(),
            reservation.ScopeId,
            TestClock.Now,
            reservation.AllocationId.Value,
            reservation.Id,
            releaseRequestId,
            reservation.AllocationVersion.Value);

        await handler.HandleAsync(released, CancellationToken.None);
        long terminalVersion = reservation.Version;
        await handler.HandleAsync(released, CancellationToken.None);

        Assert.Equal(ReservationState.NoShow, reservation.Status);
        Assert.Equal(terminalVersion, reservation.Version);
        Assert.Equal(2, projection.ReleaseCount);
    }

    private static Reservation CreateConfirmedReservation()
    {
        Reservation reservation = Reservation.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            "Ada Guest",
            "ada@example.test",
            null,
            1,
            ReservationSource.Direct,
            sourceSystem: null,
            sourceReference: null,
            notes: null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationDetailsChangeOrigin.Staff,
            initialDetailsActorId: null,
            initialAdapterConnectionId: null,
            initialExternalOperationId: null,
            Guid.NewGuid(),
            TestClock.Now).Value;
        Assert.True(reservation.ConfirmAllocation(
            reservation.AllocationRequestId,
            Guid.NewGuid(),
            allocationVersion: 1,
            Guid.NewGuid(),
            TestClock.Now).IsSuccess);
        return reservation;
    }

    private sealed class FakeReservationRepository(Reservation reservation) : IReservationRepository
    {
        public Task AddAsync(Reservation value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Reservation?> GetAsync(Guid propertyId, Guid reservationId, CancellationToken cancellationToken) =>
            Task.FromResult<Reservation?>(
                reservation.PropertyId == propertyId && reservation.Id == reservationId ? reservation : null);

        public Task<Reservation?> GetAsyncByReservationId(Guid reservationId, CancellationToken cancellationToken) =>
            Task.FromResult<Reservation?>(reservation.Id == reservationId ? reservation : null);

        public Task<Reservation?> GetByExternalSourceAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) => Task.FromResult<Reservation?>(null);

        public Task<bool> ExternalSourceExistsAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<ReservationListResponse> ListAsync(
            Guid propertyId,
            IReadOnlyCollection<ReservationStatus>? statuses,
            string? search,
            ReservationListOrder order,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ReservationListResponse([], pageRequest.Page, pageRequest.PageSize, 0));
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => ReservationsModuleMetadata.Name;
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

    private sealed class RecordingInventoryProjection : IInventoryProjectionRepository
    {
        public int ReleaseCount { get; private set; }

        public Task<InventoryUnitSelectionValidation> ValidateSelectionAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) => Task.FromResult(InventoryUnitSelectionValidation.Valid);

        public Task ApplyUnitAsync(ReservationInventoryUnitWriteModel unit, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ApplyBlockAsync(ReservationInventoryBlockWriteModel block, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReleaseBlockAsync(
            string scopeId,
            Guid propertyId,
            Guid inventoryUnitId,
            Guid blockId,
            long version,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ApplyAllocationAsync(
            ReservationInventoryAllocationWriteModel allocation,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReleaseAllocationAsync(
            string scopeId,
            Guid allocationId,
            Guid reservationId,
            long version,
            CancellationToken cancellationToken)
        {
            this.ReleaseCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(
            IReadOnlyCollection<Gma.Framework.Domain.IDomainEvent> domainEvents,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestClock : ISystemClock
    {
        public static DateTimeOffset Now { get; } = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
