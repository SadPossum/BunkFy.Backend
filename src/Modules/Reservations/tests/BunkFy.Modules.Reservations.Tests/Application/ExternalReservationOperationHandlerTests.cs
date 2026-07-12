namespace BunkFy.Modules.Reservations.Tests;

using Gma.Framework.Application.Events;
using Gma.Framework.Domain;
using Gma.Framework.Messaging;
using Gma.Framework.Pagination;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Reservations.Application.External;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ExternalReservationOperationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_is_idempotent_and_conflicting_operation_reuse_is_rejected()
    {
        FakeReservationRepository reservations = new();
        FakeOperationRepository operations = new();
        RecordingOutbox outbox = new();
        ExternalReservationOperationCoordinator coordinator = CreateCoordinator(operations, outbox);
        ExternalReservationCreateRequestedHandler handler = new(
            reservations,
            new ValidInventoryProjection(),
            coordinator,
            CreateDomainEventDispatcher(),
            new TestClock(),
            new TestIdGenerator());
        Guid operationId = Guid.NewGuid();
        ExternalReservationCreateRequestedIntegrationEvent request = CreateRequest(operationId, "Ada Guest");

        await handler.HandleAsync(request, CancellationToken.None);
        await handler.HandleAsync(request with { }, CancellationToken.None);
        await handler.HandleAsync(CreateRequest(operationId, "Different Guest"), CancellationToken.None);

        Reservation reservation = Assert.Single(reservations.Items);
        Assert.Equal(ReservationDetailsChangeOrigin.Adapter, reservation.LastDetailsChangeOrigin);
        Assert.Equal(operationId, reservation.LastDetailsExternalOperationId);
        ReservationExternalOperationRecord operation = Assert.Single(operations.Items.Values);
        Assert.Equal(ExternalReservationOperationOutcome.Applied, operation.Outcome);
        ExternalReservationOperationCompletedIntegrationEvent[] outcomes = outbox.Events
            .Cast<ExternalReservationOperationCompletedIntegrationEvent>()
            .ToArray();
        Assert.Equal(3, outcomes.Length);
        Assert.Equal(ExternalReservationOperationOutcome.Applied, outcomes[0].Outcome);
        Assert.Equal(ExternalReservationOperationOutcome.Applied, outcomes[1].Outcome);
        Assert.Equal(ExternalReservationOperationOutcome.OperationConflict, outcomes[2].Outcome);
    }

    [Fact]
    public async Task Guest_change_reports_details_conflict_after_staff_edit()
    {
        Guid connectionId = Guid.NewGuid();
        Reservation reservation = CreateExternalReservation(connectionId, Guid.NewGuid());
        reservation.ClearDomainEvents();
        reservation.UpdateGuestDetails(
            "Staff Corrected",
            reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            reservation.Notes,
            expectedDetailsRevision: 1,
            ReservationDetailsChangeOrigin.Staff,
            "user:staff-a",
            adapterConnectionId: null,
            externalOperationId: null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(1));
        FakeOperationRepository operations = new();
        RecordingOutbox outbox = new();
        ExternalReservationGuestDetailsChangeRequestedHandler handler = new(
            new FakeReservationRepository(reservation),
            CreateCoordinator(operations, outbox),
            CreateDomainEventDispatcher(),
            new TestClock(),
            new TestIdGenerator());
        ExternalReservationGuestDetailsChangeRequestedIntegrationEvent request = new(
            Guid.NewGuid(),
            "tenant-a",
            Now.AddMinutes(2),
            Guid.NewGuid(),
            Guid.NewGuid(),
            connectionId,
            reservation.PropertyId,
            reservation.Id,
            reservation.SourceSystem!,
            reservation.SourceReference!,
            expectedDetailsRevision: 1,
            "Adapter Guest",
            reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            reservation.Notes);

        await handler.HandleAsync(request, CancellationToken.None);

        Assert.Equal("Staff Corrected", reservation.PrimaryGuestName);
        Assert.Equal(2, reservation.DetailsRevision);
        Assert.Equal(
            ExternalReservationOperationOutcome.DetailsRevisionConflict,
            Assert.Single(operations.Items.Values).Outcome);
        Assert.Equal(
            ExternalReservationOperationOutcome.DetailsRevisionConflict,
            Assert.IsType<ExternalReservationOperationCompletedIntegrationEvent>(Assert.Single(outbox.Events)).Outcome);
    }

    [Fact]
    public async Task Cancellation_reports_details_conflict_after_staff_edit()
    {
        Guid connectionId = Guid.NewGuid();
        Reservation reservation = CreateExternalReservation(connectionId, Guid.NewGuid());
        reservation.ClearDomainEvents();
        reservation.UpdateGuestDetails(
            "Staff Corrected",
            reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            reservation.Notes,
            expectedDetailsRevision: 1,
            ReservationDetailsChangeOrigin.Staff,
            "user:staff-a",
            adapterConnectionId: null,
            externalOperationId: null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(1));
        FakeOperationRepository operations = new();
        RecordingOutbox outbox = new();
        ExternalReservationCancellationRequestedHandler handler = new(
            new FakeReservationRepository(reservation),
            CreateCoordinator(operations, outbox),
            CreateDomainEventDispatcher(),
            new TestClock(),
            new TestIdGenerator());
        ExternalReservationCancellationRequestedIntegrationEvent request = new(
            Guid.NewGuid(),
            "tenant-a",
            Now.AddMinutes(2),
            Guid.NewGuid(),
            Guid.NewGuid(),
            connectionId,
            reservation.PropertyId,
            reservation.Id,
            reservation.SourceSystem!,
            reservation.SourceReference!,
            expectedDetailsRevision: 1);

        await handler.HandleAsync(request, CancellationToken.None);

        Assert.Equal(ReservationState.PendingAllocation, reservation.Status);
        Assert.Equal(
            ExternalReservationOperationOutcome.DetailsRevisionConflict,
            Assert.Single(operations.Items.Values).Outcome);
        Assert.Equal(
            ExternalReservationOperationOutcome.DetailsRevisionConflict,
            Assert.IsType<ExternalReservationOperationCompletedIntegrationEvent>(Assert.Single(outbox.Events)).Outcome);
    }

    [Fact]
    public async Task Cancellation_with_current_details_revision_is_accepted_while_release_is_pending()
    {
        Guid connectionId = Guid.NewGuid();
        Reservation reservation = CreateExternalReservation(connectionId, Guid.NewGuid());
        reservation.ClearDomainEvents();
        FakeOperationRepository operations = new();
        RecordingOutbox outbox = new();
        ExternalReservationCancellationRequestedHandler handler = new(
            new FakeReservationRepository(reservation),
            CreateCoordinator(operations, outbox),
            CreateDomainEventDispatcher(),
            new TestClock(),
            new TestIdGenerator());
        ExternalReservationCancellationRequestedIntegrationEvent request = new(
            Guid.NewGuid(),
            "tenant-a",
            Now.AddMinutes(1),
            Guid.NewGuid(),
            Guid.NewGuid(),
            connectionId,
            reservation.PropertyId,
            reservation.Id,
            reservation.SourceSystem!,
            reservation.SourceReference!,
            reservation.DetailsRevision);

        await handler.HandleAsync(request, CancellationToken.None);

        Assert.Equal(ReservationState.CancellationPending, reservation.Status);
        ReservationExternalOperationRecord operation = Assert.Single(operations.Items.Values);
        Assert.Equal(ExternalReservationOperationOutcome.Accepted, operation.Outcome);
        Assert.Equal(reservation.Version, operation.ReservationVersion);
        Assert.Equal(
            ExternalReservationOperationOutcome.Accepted,
            Assert.IsType<ExternalReservationOperationCompletedIntegrationEvent>(Assert.Single(outbox.Events)).Outcome);
    }

    [Fact]
    public async Task Pending_amendment_replay_is_quiet_and_incompatible_reuse_does_not_poison_the_ledger()
    {
        Guid connectionId = Guid.NewGuid();
        Reservation reservation = CreateExternalReservation(connectionId, Guid.NewGuid());
        Assert.True(reservation.ConfirmAllocation(
            reservation.AllocationRequestId,
            Guid.NewGuid(),
            allocationVersion: 1,
            Guid.NewGuid(),
            Now).IsSuccess);
        reservation.ClearDomainEvents();
        FakeOperationRepository operations = new();
        RecordingOutbox outbox = new();
        ExternalReservationAmendmentRequestedHandler handler = new(
            new FakeReservationRepository(reservation),
            new ValidInventoryProjection(),
            CreateCoordinator(operations, outbox),
            CreateDomainEventDispatcher(),
            new TestClock(),
            new TestIdGenerator());
        Guid operationId = Guid.NewGuid();
        ExternalReservationAmendmentRequestedIntegrationEvent request = AmendmentRequest(
            reservation,
            connectionId,
            operationId,
            new DateOnly(2026, 8, 4));

        await handler.HandleAsync(request, CancellationToken.None);
        await handler.HandleAsync(request, CancellationToken.None);
        await handler.HandleAsync(
            AmendmentRequest(reservation, connectionId, operationId, new DateOnly(2026, 8, 5)),
            CancellationToken.None);

        Assert.Equal(operationId, reservation.PendingAllocationAmendmentId);
        Assert.Equal(new DateOnly(2026, 8, 4), reservation.PendingDeparture);
        Assert.Empty(operations.Items);
        ExternalReservationOperationCompletedIntegrationEvent conflict =
            Assert.IsType<ExternalReservationOperationCompletedIntegrationEvent>(Assert.Single(outbox.Events));
        Assert.Equal(ExternalReservationOperationOutcome.OperationConflict, conflict.Outcome);
    }

    private static ExternalReservationCreateRequestedIntegrationEvent CreateRequest(Guid operationId, string guestName) => new(
        Guid.NewGuid(),
        "tenant-a",
        Now,
        operationId,
        Guid.NewGuid(),
        Guid.Parse("50000000-0000-0000-0000-000000000001"),
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        "fake-http",
        "booking-42",
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 8, 3),
        [Guid.Parse("20000000-0000-0000-0000-000000000001")],
        guestName,
        "guest@example.test",
        null,
        1,
        null);

    private static ExternalReservationAmendmentRequestedIntegrationEvent AmendmentRequest(
        Reservation reservation,
        Guid connectionId,
        Guid operationId,
        DateOnly departure) => new(
        Guid.NewGuid(),
        reservation.ScopeId,
        Now,
        operationId,
        Guid.NewGuid(),
        connectionId,
        reservation.PropertyId,
        reservation.Id,
        reservation.SourceSystem!,
        reservation.SourceReference!,
        reservation.DetailsRevision,
        reservation.Arrival,
        departure,
        reservation.RequestedUnits.Select(unit => unit.InventoryUnitId).ToArray(),
        reservation.PrimaryGuestName,
        reservation.Email,
        reservation.Phone,
        reservation.GuestCount,
        reservation.Notes);

    private static Reservation CreateExternalReservation(Guid connectionId, Guid operationId) => Reservation.Create(
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
        ReservationSource.External,
        "fake-http",
        "booking-42",
        notes: null,
        Guid.NewGuid(),
        Guid.NewGuid(),
        ReservationDetailsChangeOrigin.Adapter,
        $"adapter:{connectionId:N}",
        connectionId,
        operationId,
        Guid.NewGuid(),
        Now).Value;

    private static ExternalReservationOperationCoordinator CreateCoordinator(
        FakeOperationRepository operations,
        RecordingOutbox outbox) => new(
            operations,
            new RecordingOutboxRegistry(outbox),
            new TestClock(),
            new TestIdGenerator());

    private static ReservationInboxDomainEventDispatcher CreateDomainEventDispatcher() =>
        new(new NoOpDomainEventDispatcher());

    private sealed class FakeReservationRepository : IReservationRepository
    {
        public FakeReservationRepository(params Reservation[] reservations) => this.Items.AddRange(reservations);

        public List<Reservation> Items { get; } = [];

        public Task AddAsync(Reservation reservation, CancellationToken cancellationToken)
        {
            this.Items.Add(reservation);
            return Task.CompletedTask;
        }

        public Task<Reservation?> GetAsync(Guid propertyId, Guid reservationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(item => item.PropertyId == propertyId && item.Id == reservationId));

        public Task<Reservation?> GetAsyncByReservationId(Guid reservationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(item => item.Id == reservationId));

        public Task<Reservation?> GetByExternalSourceAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(item =>
                item.SourceSystem == sourceSystem && item.SourceReference == sourceReference));

        public Task<bool> ExternalSourceExistsAsync(string sourceSystem, string sourceReference, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.Any(item => item.SourceSystem == sourceSystem && item.SourceReference == sourceReference));

        public Task<ReservationListResponse> ListAsync(
            Guid propertyId,
            ReservationStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ReservationListResponse([], pageRequest.Page, pageRequest.PageSize));
    }

    private sealed class FakeOperationRepository : IReservationExternalOperationRepository
    {
        public Dictionary<Guid, ReservationExternalOperationRecord> Items { get; } = [];

        public Task<ReservationExternalOperationRecord?> GetAsync(Guid operationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.GetValueOrDefault(operationId));

        public Task AddAsync(ReservationExternalOperationRecord operation, CancellationToken cancellationToken)
        {
            this.Items.Add(operation.OperationId, operation);
            return Task.CompletedTask;
        }
    }

    private sealed class ValidInventoryProjection : IInventoryProjectionRepository
    {
        public Task<InventoryUnitSelectionValidation> ValidateSelectionAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) => Task.FromResult(InventoryUnitSelectionValidation.Valid);

        public Task ApplyUnitAsync(ReservationInventoryUnitWriteModel unit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ApplyBlockAsync(ReservationInventoryBlockWriteModel block, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReleaseBlockAsync(string scopeId, Guid propertyId, Guid inventoryUnitId, Guid blockId, long version, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ApplyAllocationAsync(ReservationInventoryAllocationWriteModel allocation, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReleaseAllocationAsync(string scopeId, Guid allocationId, Guid reservationId, long version, CancellationToken cancellationToken) => throw new NotSupportedException();
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

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(
            IReadOnlyCollection<IDomainEvent> domainEvents,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
