namespace BunkFy.Modules.Reservations.Tests.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RestoreReservationAnonymisationCommandHandlerTests
{
    private static readonly DateTimeOffset OriginallyCompletedAtUtc =
        new(2026, 7, 26, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReplayedAtUtc =
        OriginallyCompletedAtUtc.AddHours(2);

    [Fact]
    public async Task Restore_re_scrubs_existing_reservation_and_replays_exactly()
    {
        Reservation reservation = CreateReservation();
        Assert.True(reservation.LinkGuest(
            Guid.NewGuid(),
            ReservationGuestRole.Primary,
            replaceExistingRole: false,
            reservation.Version,
            "user:front-desk",
            Guid.NewGuid(),
            OriginallyCompletedAtUtc.AddHours(-1)).IsSuccess);
        reservation.ClearDomainEvents();
        RecordingRepository repository = new(reservation);
        RecordingOperationLock operationLock = new();
        RestoreReservationAnonymisationCommandHandler handler =
            CreateHandler(repository, operationLock, Guid.NewGuid());
        DataRightsAnonymisationRestoreRequest request =
            CreateRequest(
                reservation.Id,
                reservation.PropertyId,
                reservation.Version + 1);

        Result<ReservationAnonymisationRestoreReceipt> first =
            await handler.HandleAsync(
                new RestoreReservationAnonymisationCommand(request),
                CancellationToken.None);
        Result<ReservationAnonymisationRestoreReceipt> replay =
            await handler.HandleAsync(
                new RestoreReservationAnonymisationCommand(request),
                CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value.CanonicalSha256, replay.Value.CanonicalSha256);
        Assert.True(reservation.MatchesAnonymisedState(
            request.ResultingRecordVersion!.Value,
            first.Value.ResultingDetailsRevision!.Value,
            OriginallyCompletedAtUtc));
        Assert.NotNull(repository.Tombstone);
        Assert.True(repository.Tombstone.MatchesRestore(
            reservation.Id,
            reservation.PropertyId,
            request.OwnerReceiptContractVersion,
            request.OwnerReceiptId,
            request.OwnerReceiptSha256,
            request.ResultingRecordVersion.Value,
            first.Value.ResultingDetailsRevision,
            OriginallyCompletedAtUtc,
            request.LedgerEntryId));
        Assert.Equal(1, repository.RedactionCount);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(2, operationLock.CallCount);
    }

    [Fact]
    public async Task Missing_reservation_creates_tombstone_only()
    {
        Guid reservationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        RecordingRepository repository = new(reservation: null);
        RestoreReservationAnonymisationCommandHandler handler =
            CreateHandler(
                repository,
                new RecordingOperationLock(),
                Guid.NewGuid());
        DataRightsAnonymisationRestoreRequest request =
            CreateRequest(
                reservationId,
                propertyId,
                resultingRecordVersion: 4);

        Result<ReservationAnonymisationRestoreReceipt> result =
            await handler.HandleAsync(
                new RestoreReservationAnonymisationCommand(request),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ResultingDetailsRevision);
        Assert.Null(repository.Reservation);
        Assert.NotNull(repository.Tombstone);
        Assert.Equal(0, repository.RedactionCount);
        Assert.Equal(1, repository.AddCount);
    }

    [Fact]
    public async Task Stale_version_and_changed_replay_proof_fail_closed()
    {
        Reservation reservation = CreateReservation();
        RecordingRepository repository = new(reservation);
        RestoreReservationAnonymisationCommandHandler handler =
            CreateHandler(
                repository,
                new RecordingOperationLock(),
                Guid.NewGuid(),
                Guid.NewGuid());
        DataRightsAnonymisationRestoreRequest stale =
            CreateRequest(
                reservation.Id,
                reservation.PropertyId,
                reservation.Version + 2);

        Result<ReservationAnonymisationRestoreReceipt> staleResult =
            await handler.HandleAsync(
                new RestoreReservationAnonymisationCommand(stale),
                CancellationToken.None);

        Assert.Equal(
            ReservationsApplicationErrors
                .AnonymisationRestoreProofConflict,
            staleResult.Error);
        Assert.False(reservation.IsAnonymised);
        Assert.Equal(0, repository.AddCount);

        DataRightsAnonymisationRestoreRequest valid =
            stale with
            {
                LedgerEntryId = Guid.NewGuid(),
                ResultingRecordVersion = reservation.Version + 1
            };
        Assert.True((await handler.HandleAsync(
            new RestoreReservationAnonymisationCommand(valid),
            CancellationToken.None)).IsSuccess);

        Result<ReservationAnonymisationRestoreReceipt> changedReplay =
            await handler.HandleAsync(
                new RestoreReservationAnonymisationCommand(
                    valid with
                    {
                        LedgerEntrySha256 = new string('c', 64)
                    }),
                CancellationToken.None);

        Assert.Equal(
            ReservationsApplicationErrors
                .AnonymisationRestoreProofConflict,
            changedReplay.Error);
        Assert.Equal(1, repository.AddCount);
    }

    private static RestoreReservationAnonymisationCommandHandler
        CreateHandler(
            RecordingRepository repository,
            RecordingOperationLock operationLock,
            params Guid[] eventIds) =>
        new(
            repository,
            operationLock,
            new TestScopeContext(),
            new TestClock(),
            new QueueIdGenerator(eventIds));

    private static DataRightsAnonymisationRestoreRequest CreateRequest(
        Guid reservationId,
        Guid propertyId,
        long resultingRecordVersion) =>
        new(
            DataRightsAnonymisationRestoreContract.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            TenantSequence: 8,
            LedgerEntrySha256: new string('a', 64),
            propertyId,
            ReservationsDataRightsCoordinates.Owner,
            ReservationsDataRightsCoordinates.ReservationRecordType,
            reservationId,
            OwnerReceiptContractVersion: 1,
            OwnerReceiptId: Guid.NewGuid(),
            OwnerReceiptSha256: new string('b', 64),
            resultingRecordVersion,
            OriginallyCompletedAtUtc);

    private static Reservation CreateReservation() => Reservation.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 8, 3),
        [Guid.NewGuid()],
        "Maya Chen",
        "maya@example.test",
        "+44 20 1234 5678",
        guestCount: 1,
        ReservationSource.External,
        sourceSystem: "booking.com",
        sourceReference: "source-123",
        notes: "Late arrival",
        eventId: Guid.NewGuid(),
        detailsEventId: Guid.NewGuid(),
        initialDetailsOrigin: ReservationDetailsChangeOrigin.Staff,
        initialDetailsActorId: "user:creator",
        initialAdapterConnectionId: null,
        initialExternalOperationId: null,
        initialCorrelationId: Guid.NewGuid(),
        nowUtc: OriginallyCompletedAtUtc.AddDays(-1),
        expectedArrivalTime: new TimeOnly(15, 0),
        expectedDepartureTime: new TimeOnly(11, 0)).Value;

    private sealed class RecordingRepository(Reservation? reservation)
        : IReservationAnonymisationRestoreRepository
    {
        public Reservation? Reservation { get; } = reservation;
        public ReservationAnonymisationReceipt? OriginalReceipt
        {
            get;
            init;
        }

        public ReservationAnonymisationTombstone? Tombstone
        {
            get;
            private set;
        }

        public ReservationAnonymisationRestoreReceipt? RestoreReceipt
        {
            get;
            private set;
        }

        public int RedactionCount { get; private set; }
        public int AddCount { get; private set; }

        public Task<Reservation?> GetReservationAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Reservation?.PropertyId == propertyId &&
                this.Reservation.Id == reservationId
                    ? this.Reservation
                    : null);

        public Task<ReservationAnonymisationReceipt?>
            GetOriginalReceiptAsync(
                Guid receiptId,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                this.OriginalReceipt?.Id == receiptId
                    ? this.OriginalReceipt
                    : null);

        public Task<ReservationAnonymisationTombstone?>
            GetTombstoneAsync(
                Guid reservationId,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Tombstone?.Id == reservationId
                    ? this.Tombstone
                    : null);

        public Task<ReservationAnonymisationRestoreReceipt?>
            GetRestoreReceiptAsync(
                Guid ledgerEntryId,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                this.RestoreReceipt?.LedgerEntryId == ledgerEntryId
                    ? this.RestoreReceipt
                    : null);

        public Task<ReservationAnonymisationAffectedRecords>
            RedactRestoredOwnedRecordsAsync(
                Reservation value,
                ReservationAnonymisationRestoreOutcome? outcome,
                CancellationToken cancellationToken)
        {
            this.RedactionCount++;
            return Task.FromResult(
                new ReservationAnonymisationAffectedRecords(
                    RedactedHistoryCount: outcome is null ? 0 : 1,
                    ReducedExternalOperationCount: 0,
                    SuppressedReminderCount: 0));
        }

        public Task<bool> VerifyRestoredOwnerStateAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task AddRestoreProofAsync(
            ReservationAnonymisationRestoreReceipt receipt,
            ReservationAnonymisationTombstone? newTombstone,
            CancellationToken cancellationToken)
        {
            this.RestoreReceipt = receipt;
            this.Tombstone ??= newTombstone;
            this.AddCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOperationLock
        : IReservationOperationLock
    {
        public int CallCount { get; private set; }

        public Task<bool> TryAcquireExistingAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            this.CallCount++;
            return Task.FromResult(true);
        }

        public Task AcquireCoordinateAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            this.CallCount++;
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
        public DateTimeOffset UtcNow => ReplayedAtUtc;
    }

    private sealed class QueueIdGenerator(params Guid[] ids) : IIdGenerator
    {
        private readonly Queue<Guid> values = new(ids);

        public Guid NewId() => this.values.Dequeue();
    }
}
