namespace BunkFy.Modules.Reservations.Tests;

using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Events;
using Xunit;

[Trait("Category", "Unit")]
public sealed class UpdateReservationGuestDetailsCommandHandlerTests
{
    [Fact]
    public async Task Policy_denial_prevents_guest_detail_mutation()
    {
        Reservation reservation = CreateReservation();
        FakeReservationManagementOperationRepository operations = new();
        UpdateReservationGuestDetailsCommandHandler handler = CreateHandler(
            reservation,
            operations,
            new FakeReservationDetailsHistoryReader(),
            policyAllowed: false);

        Result<ReservationMutationReceiptDto> result = await handler.HandleAsync(
            Command(reservation, Guid.NewGuid(), primaryGuestName: "Blocked Change"),
            CancellationToken.None);

        Assert.Equal("Reservations.CountryPolicyDenied.MissingBinding", result.Error.Code);
        Assert.Equal("Ada Guest", reservation.PrimaryGuestName);
        Assert.Equal(1, reservation.DetailsRevision);
        Assert.Empty(operations.Items);
    }

    [Fact]
    public async Task Staff_update_changes_details_revision_without_using_lifecycle_version_as_authority()
    {
        Reservation reservation = CreateReservation();
        Assert.True(reservation.ConfirmAllocation(
            reservation.AllocationRequestId,
            Guid.NewGuid(),
            allocationVersion: 1,
            Guid.NewGuid(),
            TestClock.Now).IsSuccess);
        Assert.Equal(2, reservation.Version);
        Assert.Equal(1, reservation.DetailsRevision);
        reservation.ClearDomainEvents();
        FakeReservationManagementOperationRepository operations = new();
        UpdateReservationGuestDetailsCommandHandler handler = CreateHandler(
            reservation,
            operations,
            new FakeReservationDetailsHistoryReader());
        Guid operationId = Guid.NewGuid();

        Result<ReservationMutationReceiptDto> result = await handler.HandleAsync(
            new UpdateReservationGuestDetailsCommand(
                operationId,
                reservation.PropertyId,
                reservation.Id,
                "Grace Guest",
                "grace@example.test",
                null,
                2,
                "Upper bunk",
                ExpectedDetailsRevision: 1,
                ReservationDetailsChangeOriginKind.Staff,
                "user:user-a"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.DetailsRevision);
        Assert.Equal(3, result.Value.Version);
        Assert.Equal(ReservationDetailsChangeOrigin.Staff, reservation.LastDetailsChangeOrigin);
        ReservationDetailsChangedDomainEvent changed = Assert.IsType<ReservationDetailsChangedDomainEvent>(
            Assert.Single(reservation.DomainEvents, item => item is ReservationDetailsChangedDomainEvent));
        Assert.Equal(operationId, changed.CorrelationId);
        ReservationManagementOperationRecord operation = Assert.Single(operations.Items);
        Assert.True(operation.MatchesGuestDetails(expectedDetailsRevision: 1));
    }

    [Fact]
    public async Task Exact_replay_returns_current_receipt_without_repeating_change()
    {
        Reservation reservation = CreateReservation();
        reservation.ClearDomainEvents();
        FakeReservationManagementOperationRepository operations = new();
        FakeReservationDetailsHistoryReader history = new();
        CountingIdGenerator ids = new();
        UpdateReservationGuestDetailsCommandHandler handler = CreateHandler(
            reservation,
            operations,
            history,
            ids: ids);
        Guid operationId = Guid.NewGuid();
        UpdateReservationGuestDetailsCommand command = Command(
            reservation,
            operationId,
            primaryGuestName: "  Grace Guest  ",
            email: " grace@example.test ",
            notes: "  Upper bunk  ");

        Result<ReservationMutationReceiptDto> first = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        ReservationDetailsChangedDomainEvent changed = Assert.IsType<ReservationDetailsChangedDomainEvent>(
            Assert.Single(reservation.DomainEvents, item => item is ReservationDetailsChangedDomainEvent));
        history.Replay = ToReplay(changed);
        reservation.ClearDomainEvents();
        Assert.True(reservation.ConfirmAllocation(
            reservation.AllocationRequestId,
            Guid.NewGuid(),
            allocationVersion: 1,
            Guid.NewGuid(),
            TestClock.Now.AddMinutes(1)).IsSuccess);
        reservation.ClearDomainEvents();

        Result<ReservationMutationReceiptDto> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(reservation.Version, replay.Value.Version);
        Assert.Equal(2, replay.Value.DetailsRevision);
        Assert.Equal(1, ids.Count);
        Assert.Single(operations.Items);
        Assert.Empty(reservation.DomainEvents);
    }

    [Fact]
    public async Task Reusing_operation_id_for_changed_details_conflicts()
    {
        Reservation reservation = CreateReservation();
        reservation.ClearDomainEvents();
        FakeReservationManagementOperationRepository operations = new();
        FakeReservationDetailsHistoryReader history = new();
        UpdateReservationGuestDetailsCommandHandler handler = CreateHandler(
            reservation,
            operations,
            history);
        Guid operationId = Guid.NewGuid();
        UpdateReservationGuestDetailsCommand command = Command(
            reservation,
            operationId,
            primaryGuestName: "Grace Guest");
        Result<ReservationMutationReceiptDto> first = await handler.HandleAsync(
            command,
            CancellationToken.None);
        ReservationDetailsChangedDomainEvent changed = Assert.IsType<ReservationDetailsChangedDomainEvent>(
            Assert.Single(reservation.DomainEvents, item => item is ReservationDetailsChangedDomainEvent));
        history.Replay = ToReplay(changed);
        reservation.ClearDomainEvents();

        Result<ReservationMutationReceiptDto> conflict = await handler.HandleAsync(
            command with { Email = "different@example.test" },
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.Equal(ReservationsApplicationErrors.ManagementOperationConflict, conflict.Error);
        Assert.Empty(reservation.DomainEvents);
    }

    [Fact]
    public async Task No_change_does_not_bind_operation_id()
    {
        Reservation reservation = CreateReservation();
        reservation.ClearDomainEvents();
        FakeReservationManagementOperationRepository operations = new();
        UpdateReservationGuestDetailsCommandHandler handler = CreateHandler(
            reservation,
            operations,
            new FakeReservationDetailsHistoryReader());
        Guid operationId = Guid.NewGuid();

        Result<ReservationMutationReceiptDto> noChange = await handler.HandleAsync(
            Command(reservation, operationId),
            CancellationToken.None);
        Result<ReservationMutationReceiptDto> changed = await handler.HandleAsync(
            Command(reservation, operationId, primaryGuestName: "Grace Guest"),
            CancellationToken.None);

        Assert.True(noChange.IsSuccess, noChange.Error.Code);
        Assert.True(changed.IsSuccess, changed.Error.Code);
        Assert.Single(operations.Items);
        Assert.Equal("Grace Guest", reservation.PrimaryGuestName);
    }

    [Fact]
    public async Task Replay_is_read_only_after_reservation_lock_and_reload()
    {
        List<string> trace = [];
        Reservation reservation = CreateReservation();
        Guid operationId = Guid.NewGuid();
        UpdateReservationGuestDetailsCommand command = Command(
            reservation,
            operationId,
            primaryGuestName: "Grace Guest");
        FakeReservationManagementOperationRepository operations = new(trace);
        operations.Items.Add(new(
            operationId,
            reservation.ScopeId,
            reservation.PropertyId,
            reservation.Id,
            ReservationManagementOperationKind.GuestDetails,
            ExpectedVersion: null,
            reservation.DetailsRevision,
            BusinessDate: null,
            TestClock.Now));
        FakeReservationDetailsHistoryReader history = new(trace)
        {
            Replay = new(
                reservation.DetailsRevision,
                ReservationDetailsChangeOriginKind.Staff,
                new(
                    reservation.Arrival,
                    reservation.Departure,
                    reservation.ExpectedArrivalTime,
                    reservation.ExpectedDepartureTime,
                    reservation.RequestedUnits.Select(unit => unit.InventoryUnitId).ToArray(),
                    "Grace Guest",
                    reservation.Email,
                    reservation.Phone,
                    reservation.GuestCount,
                    reservation.Notes))
        };
        UpdateReservationGuestDetailsCommandHandler handler = new(
            ReservationMutationTestSupport.Create(
                new FakeReservationRepository(reservation, trace),
                new RecordingReservationOperationLock(trace),
                new TestReservationScopeContext()),
            new TestReservationCountryPolicyAdmission(),
            operations,
            history,
            new TestClock(),
            new TestIdGenerator());

        Result<ReservationMutationReceiptDto> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(["lock", "reservation", "journal", "history"], trace);
    }

    [Fact]
    public async Task Replay_rejects_invalid_actor_provenance_before_reading_witness()
    {
        List<string> trace = [];
        Reservation reservation = CreateReservation();
        Guid operationId = Guid.NewGuid();
        FakeReservationManagementOperationRepository operations = new(trace);
        operations.Items.Add(new(
            operationId,
            reservation.ScopeId,
            reservation.PropertyId,
            reservation.Id,
            ReservationManagementOperationKind.GuestDetails,
            ExpectedVersion: null,
            reservation.DetailsRevision,
            BusinessDate: null,
            TestClock.Now));
        UpdateReservationGuestDetailsCommandHandler handler = CreateHandler(
            reservation,
            operations,
            new FakeReservationDetailsHistoryReader(trace));

        Result<ReservationMutationReceiptDto> result = await handler.HandleAsync(
            Command(reservation, operationId) with { ActorId = "   " },
            CancellationToken.None);

        Assert.Equal(ReservationsApplicationErrors.DetailsChangeProvenanceInvalid, result.Error);
        Assert.Empty(trace);
    }

    [Fact]
    public async Task Management_command_cannot_impersonate_adapter_origin()
    {
        Reservation reservation = CreateReservation();
        UpdateReservationGuestDetailsCommandHandler handler = CreateHandler(
            reservation,
            new FakeReservationManagementOperationRepository(),
            new FakeReservationDetailsHistoryReader());

        Result<ReservationMutationReceiptDto> result = await handler.HandleAsync(
            Command(reservation, Guid.NewGuid()) with
            {
                Origin = ReservationDetailsChangeOriginKind.Adapter,
                ActorId = "service:adapter"
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Reservations.DetailsChangeProvenanceInvalid", result.Error.Code);
    }

    private static UpdateReservationGuestDetailsCommandHandler CreateHandler(
        Reservation reservation,
        IReservationManagementOperationRepository operations,
        IReservationDetailsHistoryReader history,
        bool policyAllowed = true,
        IIdGenerator? ids = null) => new(
            ReservationMutationTestSupport.Create(new FakeReservationRepository(reservation)),
            new TestReservationCountryPolicyAdmission(policyAllowed),
            operations,
            history,
            new TestClock(),
            ids ?? new TestIdGenerator());

    private static UpdateReservationGuestDetailsCommand Command(
        Reservation reservation,
        Guid operationId,
        string? primaryGuestName = null,
        string? email = null,
        string? notes = null) => new(
            operationId,
            reservation.PropertyId,
            reservation.Id,
            primaryGuestName ?? reservation.PrimaryGuestName,
            email ?? reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            notes ?? reservation.Notes,
            reservation.DetailsRevision,
            ReservationDetailsChangeOriginKind.Staff,
            "user:staff",
            reservation.ExpectedArrivalTime,
            reservation.ExpectedDepartureTime);

    private static ReservationDetailsOperationReplay ToReplay(
        ReservationDetailsChangedDomainEvent changed) => new(
            changed.FromRevision,
            (ReservationDetailsChangeOriginKind)(int)changed.Origin,
            new(
                changed.After.Arrival,
                changed.After.Departure,
                changed.After.ExpectedArrivalTime,
                changed.After.ExpectedDepartureTime,
                changed.After.InventoryUnitIds,
                changed.After.PrimaryGuestName,
                changed.After.Email,
                changed.After.Phone,
                changed.After.GuestCount,
                changed.After.Notes));

    private static Reservation CreateReservation() => Reservation.Create(
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

    private sealed class FakeReservationRepository(
        Reservation reservation,
        List<string>? trace = null) : IReservationRepository
    {
        public Task AddAsync(Reservation value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Reservation?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            trace?.Add("reservation");
            return Task.FromResult<Reservation?>(
                reservation.PropertyId == propertyId && reservation.Id == reservationId
                    ? reservation
                    : null);
        }

        public Task<Reservation?> GetForDataRightsAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            this.GetAsync(propertyId, reservationId, cancellationToken);

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
            Task.FromResult(new ReservationListResponse([], pageRequest.Page, pageRequest.PageSize, false));
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

    private sealed class CountingIdGenerator : IIdGenerator
    {
        public int Count { get; private set; }

        public Guid NewId()
        {
            this.Count++;
            return Guid.NewGuid();
        }
    }

    private sealed class FakeReservationManagementOperationRepository(
        List<string>? trace = null) : IReservationManagementOperationRepository
    {
        public List<ReservationManagementOperationRecord> Items { get; } = [];

        public Task<ReservationManagementOperationRecord?> GetAsync(
            Guid reservationId,
            Guid operationId,
            CancellationToken cancellationToken)
        {
            trace?.Add("journal");
            return Task.FromResult(this.Items.SingleOrDefault(item =>
                item.ReservationId == reservationId &&
                item.OperationId == operationId));
        }

        public Task AddAsync(
            ReservationManagementOperationRecord operation,
            CancellationToken cancellationToken)
        {
            this.Items.Add(operation);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReservationDetailsHistoryReader(List<string>? trace = null)
        : IReservationDetailsHistoryReader
    {
        public ReservationDetailsOperationReplay? Replay { get; set; }

        public Task<ReservationDetailsOperationReplay?> FindOperationAsync(
            Guid propertyId,
            Guid reservationId,
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            trace?.Add("history");
            return Task.FromResult(this.Replay);
        }

        public Task<ReservationDetailsHistoryListResponse> ListAsync(
            Guid propertyId,
            Guid reservationId,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => Task.FromResult(
                new ReservationDetailsHistoryListResponse(
                    [],
                    pageRequest.Page,
                    pageRequest.PageSize,
                    HasMore: false));
    }

    private sealed class RecordingReservationOperationLock(List<string> trace)
        : IReservationOperationLock
    {
        public Task<bool> TryAcquireExistingAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            trace.Add("lock");
            return Task.FromResult(true);
        }

        public Task AcquireCoordinateAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestReservationScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
