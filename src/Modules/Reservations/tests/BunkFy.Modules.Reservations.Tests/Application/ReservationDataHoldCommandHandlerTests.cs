namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationDataHoldCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Place_and_equivalent_retry_return_the_same_receipt()
    {
        Reservation reservation = CreateReservation();
        RecordingHoldRepository holds = new();
        RecordingOperationLock operationLock = new();
        PlaceReservationDataHoldCommandHandler handler = new(
            ReservationMutationTestSupport.Create(
                new StubReservationRepository(reservation),
                operationLock),
            holds,
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        PlaceReservationDataHoldCommand command = new(
            Guid.NewGuid(),
            reservation.PropertyId,
            reservation.Id,
            reservation.Version,
            reservation.DetailsRevision,
            ReservationDataHoldReasonCodes.RegulatoryRequest,
            "user:privacy");

        Result<ReservationDataHoldReceiptDto> first =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<ReservationDataHoldReceiptDto> replay =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Single(holds.Holds);
        Assert.Single(holds.Receipts);
        Assert.Single(operationLock.ReservationIds);
    }

    [Fact]
    public async Task Changed_retry_and_stale_details_revision_are_rejected()
    {
        Reservation reservation = CreateReservation();
        RecordingHoldRepository holds = new();
        PlaceReservationDataHoldCommandHandler handler = new(
            ReservationMutationTestSupport.Create(
                new StubReservationRepository(reservation),
                new RecordingOperationLock()),
            holds,
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        PlaceReservationDataHoldCommand command = new(
            Guid.NewGuid(),
            reservation.PropertyId,
            reservation.Id,
            reservation.Version,
            reservation.DetailsRevision,
            ReservationDataHoldReasonCodes.Dispute,
            "user:privacy");

        Assert.True((await handler.HandleAsync(
            command,
            CancellationToken.None)).IsSuccess);
        Result<ReservationDataHoldReceiptDto> changed =
            await handler.HandleAsync(
                command with
                {
                    ReasonCode =
                        ReservationDataHoldReasonCodes.LegalObligation
                },
                CancellationToken.None);
        Result<ReservationDataHoldReceiptDto> stale =
            await handler.HandleAsync(
                command with
                {
                    IdempotencyKey = Guid.NewGuid(),
                    ExpectedDetailsRevision = reservation.DetailsRevision + 1
                },
                CancellationToken.None);

        Assert.Equal(
            ReservationsApplicationErrors.DataHoldIdempotencyConflict,
            changed.Error);
        Assert.Equal(
            ReservationsApplicationErrors.DataHoldDetailsRevisionConflict,
            stale.Error);
        Assert.Single(holds.Holds);
    }

    [Fact]
    public async Task Release_requires_exact_hold_version_and_replays_exactly()
    {
        Reservation reservation = CreateReservation();
        ReservationDataHold hold = ReservationDataHold.Place(
            Guid.NewGuid(),
            reservation.ScopeId,
            reservation.PropertyId,
            reservation.Id,
            ReservationDataHoldReasonCodes.SecurityInvestigation,
            "user:privacy",
            Now.AddMinutes(-1)).Value;
        RecordingHoldRepository holds = new([hold]);
        ReleaseReservationDataHoldCommandHandler handler = new(
            ReservationMutationTestSupport.Create(
                new StubReservationRepository(reservation),
                new RecordingOperationLock()),
            holds,
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        ReleaseReservationDataHoldCommand command = new(
            Guid.NewGuid(),
            reservation.PropertyId,
            reservation.Id,
            hold.Id,
            reservation.Version,
            reservation.DetailsRevision,
            hold.Version,
            "user:decision-maker");

        Result<ReservationDataHoldReceiptDto> released =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<ReservationDataHoldReceiptDto> replay =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(released.IsSuccess);
        Assert.Equal(released.Value, replay.Value);
        Assert.Equal(ReservationDataHoldState.Released, hold.State);
        Assert.Single(holds.Receipts);
    }

    private static Reservation CreateReservation() => Reservation.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 8, 3),
        [Guid.NewGuid()],
        "Original Guest",
        "original@example.test",
        "+44 20 1234 5678",
        guestCount: 1,
        ReservationSource.Direct,
        sourceSystem: null,
        sourceReference: null,
        notes: "Late arrival",
        eventId: Guid.NewGuid(),
        detailsEventId: Guid.NewGuid(),
        initialDetailsOrigin: ReservationDetailsChangeOrigin.Staff,
        initialDetailsActorId: "user:creator",
        initialAdapterConnectionId: null,
        initialExternalOperationId: null,
        initialCorrelationId: Guid.NewGuid(),
        nowUtc: Now.AddDays(-1),
        expectedArrivalTime: new TimeOnly(15, 0),
        expectedDepartureTime: new TimeOnly(11, 0)).Value;

    private sealed class StubReservationRepository(Reservation reservation)
        : IReservationRepository
    {
        public Task AddAsync(
            Reservation value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Reservation?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Reservation?>(null);

        public Task<Reservation?> GetForDataRightsAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Reservation?>(
                reservation.PropertyId == propertyId &&
                reservation.Id == reservationId
                    ? reservation
                    : null);

        public Task<Reservation?> GetAsyncByReservationId(
            Guid reservationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Reservation?> GetByExternalSourceAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExternalSourceExistsAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReservationListResponse> ListAsync(
            Guid propertyId,
            IReadOnlyCollection<ReservationStatus>? statuses,
            string? search,
            ReservationListOrder order,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHoldRepository(
        IEnumerable<ReservationDataHold>? initial = null)
        : IReservationDataHoldRepository
    {
        public List<ReservationDataHold> Holds { get; } =
            initial?.ToList() ?? [];
        public List<ReservationDataHoldReceipt> Receipts { get; } = [];

        public Task<ReservationDataHoldReceipt?> FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Receipts.SingleOrDefault(
                receipt => receipt.IdempotencyKey == idempotencyKey));

        public Task<ReservationDataHold?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            Guid holdId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Holds.SingleOrDefault(hold =>
                hold.PropertyId == propertyId &&
                hold.ReservationId == reservationId &&
                hold.Id == holdId));

        public Task<IReadOnlyCollection<ReservationDataHold>> ListAsync(
            Guid propertyId,
            Guid reservationId,
            ReservationDataHoldStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(
            ReservationDataHold hold,
            CancellationToken cancellationToken)
        {
            this.Holds.Add(hold);
            return Task.CompletedTask;
        }

        public Task AddReceiptAsync(
            ReservationDataHoldReceipt receipt,
            CancellationToken cancellationToken)
        {
            this.Receipts.Add(receipt);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOperationLock : IReservationOperationLock
    {
        public List<Guid> ReservationIds { get; } = [];

        public Task<bool> TryAcquireExistingAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            this.ReservationIds.Add(reservationId);
            return Task.FromResult(true);
        }

        public Task AcquireCoordinateAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            this.ReservationIds.Add(reservationId);
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

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
