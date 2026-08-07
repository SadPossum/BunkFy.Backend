namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;
using ContractReviewReason = BunkFy.Modules.Reservations.Contracts.ReservationGuestRecordLinkReviewReason;
using DomainReviewReason = BunkFy.Modules.Reservations.Domain.GuestRecords.ReservationGuestRecordLinkReviewReason;

[Trait("Category", "Unit")]
public sealed class ReservationGuestRecordLinkProcessHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Prepare_replays_exactly_and_rejects_rebinding()
    {
        Reservation reservation = CreateReservation();
        FakeProcessRepository processes = new();
        PrepareReservationGuestRecordLinkCommandHandler handler = new(
            ReservationMutationTestSupport.Create(
                new FakeReservationRepository(reservation)),
            processes,
            new TestReservationCountryPolicyAdmission(),
            new TestScopeContext(),
            new TestClock(Now),
            new TestIdGenerator());
        PrepareReservationGuestRecordLinkCommand command = new(
            Guid.NewGuid(),
            reservation.PropertyId,
            reservation.Id,
            reservation.Version,
            "user:staff");

        Result<ReservationGuestRecordLinkPreparationDto> created =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<ReservationGuestRecordLinkPreparationDto> replayed =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<ReservationGuestRecordLinkPreparationDto> rebound =
            await handler.HandleAsync(
                command with
                {
                    ExpectedReservationVersion = command.ExpectedReservationVersion + 1
                },
                CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Equal(
            created.Value.CreationConfirmationId,
            replayed.Value.CreationConfirmationId);
        Assert.Equal(1, processes.AddCount);
        Assert.Equal(
            "Reservations.GuestRecordLinkProcessConflict",
            rebound.Error.Code);
    }

    [Fact]
    public async Task Prepare_with_a_fresh_operation_resumes_the_reservation_process()
    {
        Reservation reservation = CreateReservation();
        ReservationGuestRecordLinkProcess process =
            ReservationGuestRecordLinkProcess.Prepare(
                Guid.NewGuid(),
                "tenant-a",
                reservation.PropertyId,
                reservation.Id,
                Guid.NewGuid(),
                reservation.Version,
                "user:staff",
                Now).Value;
        FakeProcessRepository processes = new(process);
        PrepareReservationGuestRecordLinkCommandHandler handler = new(
            ReservationMutationTestSupport.Create(
                new FakeReservationRepository(reservation)),
            processes,
            new TestReservationCountryPolicyAdmission(),
            new TestScopeContext(),
            new TestClock(Now.AddMinutes(1)),
            new TestIdGenerator());

        Result<ReservationGuestRecordLinkPreparationDto> resumed =
            await handler.HandleAsync(
                new(
                    Guid.NewGuid(),
                    reservation.PropertyId,
                    reservation.Id,
                    reservation.Version + 1,
                    "user:another-staff"),
                CancellationToken.None);

        Assert.True(resumed.IsSuccess);
        Assert.Equal(process.Id, resumed.Value.Process.OperationId);
        Assert.Equal(
            process.CreationConfirmationId,
            resumed.Value.CreationConfirmationId);
        Assert.Equal(0, processes.AddCount);
    }

    [Fact]
    public async Task Prepare_refuses_to_compete_with_a_current_primary_guest()
    {
        Reservation reservation = CreateReservation();
        Assert.True(reservation.LinkGuest(
            Guid.NewGuid(),
            ReservationGuestRole.Primary,
            replaceExistingRole: false,
            reservation.Version,
            "user:staff",
            Guid.NewGuid(),
            Now).IsSuccess);
        PrepareReservationGuestRecordLinkCommandHandler handler = new(
            ReservationMutationTestSupport.Create(
                new FakeReservationRepository(reservation)),
            new FakeProcessRepository(),
            new TestReservationCountryPolicyAdmission(),
            new TestScopeContext(),
            new TestClock(Now.AddMinutes(1)),
            new TestIdGenerator());

        Result<ReservationGuestRecordLinkPreparationDto> result =
            await handler.HandleAsync(
                new(
                    Guid.NewGuid(),
                    reservation.PropertyId,
                    reservation.Id,
                    reservation.Version,
                    "user:staff"),
                CancellationToken.None);

        Assert.Equal(
            "Reservations.GuestRecordLinkProcessReservationOccupied",
            result.Error.Code);
    }

    [Fact]
    public async Task Advance_safely_rebases_while_empty_and_completes_the_link()
    {
        Reservation reservation = CreateReservation();
        ReservationGuestRecordLinkProcess process = CreateReadyProcess(reservation);
        Assert.True(reservation.UpdateGuestDetails(
            "Updated Guest",
            reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            reservation.Notes,
            reservation.DetailsRevision,
            ReservationDetailsChangeOrigin.Staff,
            "user:editor",
            adapterConnectionId: null,
            externalOperationId: null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(reservation.Version > process.ExpectedReservationVersion);
        reservation.ClearDomainEvents();

        AdvanceReservationGuestRecordLinkCommandHandler handler = CreateAdvanceHandler(
            reservation,
            process,
            linkable: true,
            GuestProcessingRestrictionGateResult.Allowed(
                GuestProcessingRestrictionContract.CurrentVersion,
                projectionRevision: 1));
        Result<ReservationGuestRecordLinkProcessDto> result =
            await handler.HandleAsync(
                AdvanceCommand(process, isFinalAttempt: false),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationGuestRecordLinkStatus.Completed, result.Value.Status);
        Assert.Null(process.RequestedBy);
        Assert.Contains(reservation.Guests, guest =>
            guest.IsCurrent && guest.GuestId == process.Id);
        Assert.Single(reservation.DomainEvents);
    }

    [Fact]
    public async Task Advance_retries_missing_projection_then_records_bounded_review()
    {
        Reservation reservation = CreateReservation();
        ReservationGuestRecordLinkProcess process = CreateReadyProcess(reservation);
        AdvanceReservationGuestRecordLinkCommandHandler handler = CreateAdvanceHandler(
            reservation,
            process,
            linkable: false,
            GuestProcessingRestrictionGateResult.Unknown);

        Result<ReservationGuestRecordLinkProcessDto> pending =
            await handler.HandleAsync(
                AdvanceCommand(process, isFinalAttempt: false),
                CancellationToken.None);
        Assert.Equal(
            "Reservations.GuestRecordLinkProcessPending",
            pending.Error.Code);
        Assert.Equal(ReservationGuestRecordLinkProcessState.Ready, process.State);

        Result<ReservationGuestRecordLinkProcessDto> terminal =
            await handler.HandleAsync(
                AdvanceCommand(process, isFinalAttempt: true),
                CancellationToken.None);
        Assert.True(terminal.IsSuccess);
        Assert.Equal(ReservationGuestRecordLinkStatus.NeedsReview, terminal.Value.Status);
        Assert.Equal(
            ContractReviewReason.GuestUnavailable,
            terminal.Value.ReviewReason);
    }

    [Fact]
    public async Task Advance_never_replaces_a_concurrently_linked_primary_guest()
    {
        Reservation reservation = CreateReservation();
        ReservationGuestRecordLinkProcess process = CreateReadyProcess(reservation);
        Guid otherGuestId = Guid.NewGuid();
        Assert.True(reservation.LinkGuest(
            otherGuestId,
            ReservationGuestRole.Primary,
            replaceExistingRole: false,
            reservation.Version,
            "user:other-staff",
            Guid.NewGuid(),
            Now.AddMinutes(2)).IsSuccess);
        reservation.ClearDomainEvents();
        AdvanceReservationGuestRecordLinkCommandHandler handler = CreateAdvanceHandler(
            reservation,
            process,
            linkable: true,
            GuestProcessingRestrictionGateResult.Allowed(
                GuestProcessingRestrictionContract.CurrentVersion,
                projectionRevision: 1));

        Result<ReservationGuestRecordLinkProcessDto> result =
            await handler.HandleAsync(
                AdvanceCommand(process, isFinalAttempt: false),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationGuestRecordLinkStatus.NeedsReview, result.Value.Status);
        Assert.Equal(
            ContractReviewReason.PrimaryGuestOccupied,
            result.Value.ReviewReason);
        Assert.Contains(reservation.Guests, guest =>
            guest.IsCurrent && guest.GuestId == otherGuestId);
        Assert.DoesNotContain(reservation.Guests, guest =>
            guest.IsCurrent && guest.GuestId == process.Id);
        Assert.Empty(reservation.DomainEvents);
    }

    [Fact]
    public async Task Retry_redispatches_review_once_and_replays_ready_state()
    {
        Reservation reservation = CreateReservation();
        ReservationGuestRecordLinkProcess process = CreateReadyProcess(reservation);
        Assert.True(process.RequireReview(
            DomainReviewReason.GuestUnavailable,
            Now.AddMinutes(2)).Value);
        process.ClearDomainEvents();
        RetryReservationGuestRecordLinkCommandHandler handler = new(
            ReservationMutationTestSupport.Create(
                new FakeReservationRepository(reservation)),
            new FakeProcessRepository(process),
            new TestReservationCountryPolicyAdmission(),
            new TestScopeContext(),
            new TestClock(Now.AddMinutes(3)),
            new TestIdGenerator());
        RetryReservationGuestRecordLinkCommand command = new(
            process.Id,
            process.PropertyId,
            process.ReservationId);

        Result<ReservationGuestRecordLinkProcessDto> retried =
            await handler.HandleAsync(command, CancellationToken.None);
        int dispatchRevision = process.DispatchRevision;
        long revision = process.Revision;
        Assert.True(retried.IsSuccess);
        Assert.Equal(ReservationGuestRecordLinkStatus.Ready, retried.Value.Status);
        Assert.Equal(ContractReviewReason.None, retried.Value.ReviewReason);
        Assert.Equal(2, dispatchRevision);
        Assert.Single(process.DomainEvents);

        process.ClearDomainEvents();
        Result<ReservationGuestRecordLinkProcessDto> replayed =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(replayed.IsSuccess);
        Assert.Equal(dispatchRevision, process.DispatchRevision);
        Assert.Equal(revision, process.Revision);
        Assert.Empty(process.DomainEvents);
    }

    private static AdvanceReservationGuestRecordLinkCommandHandler CreateAdvanceHandler(
        Reservation reservation,
        ReservationGuestRecordLinkProcess process,
        bool linkable,
        GuestProcessingRestrictionGateResult restriction) => new(
        ReservationMutationTestSupport.Create(
            new FakeReservationRepository(reservation)),
        new FakeProcessRepository(process),
        new FakeGuestProjectionRepository(linkable),
        new FakeRestrictionGate(restriction),
        new TestReservationCountryPolicyAdmission(),
        new TestScopeContext(),
        new TestClock(Now.AddMinutes(3)),
        new TestIdGenerator());

    private static AdvanceReservationGuestRecordLinkCommand AdvanceCommand(
        ReservationGuestRecordLinkProcess process,
        bool isFinalAttempt) => new(
        process.Id,
        process.PropertyId,
        process.ReservationId,
        process.DispatchRevision,
        isFinalAttempt);

    private static ReservationGuestRecordLinkProcess CreateReadyProcess(
        Reservation reservation)
    {
        Guid confirmationId = Guid.NewGuid();
        ReservationGuestRecordLinkProcess process =
            ReservationGuestRecordLinkProcess.Prepare(
                Guid.NewGuid(),
                "tenant-a",
                reservation.PropertyId,
                reservation.Id,
                confirmationId,
                reservation.Version,
                "user:staff",
                Now).Value;
        Assert.True(process.ConfirmGuest(
            confirmationId,
            Guid.NewGuid(),
            Now.AddMinutes(1)).Value);
        process.ClearDomainEvents();
        return process;
    }

    private static Reservation CreateReservation() => Reservation.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 8, 10),
        new DateOnly(2026, 8, 12),
        [Guid.NewGuid()],
        "Guest",
        "guest@example.test",
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
        Now).Value;

    private sealed class FakeProcessRepository(
        ReservationGuestRecordLinkProcess? process = null)
        : IReservationGuestRecordLinkProcessRepository
    {
        public ReservationGuestRecordLinkProcess? Process { get; private set; } = process;
        public int AddCount { get; private set; }

        public Task<ReservationGuestRecordLinkProcess?> GetByOperationAsync(
            Guid operationId,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Process?.Id == operationId ? this.Process : null);

        public Task<ReservationGuestRecordLinkProcess?> GetByReservationAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Process?.PropertyId == propertyId &&
            this.Process.ReservationId == reservationId
                ? this.Process
                : null);

        public Task<ReservationGuestRecordLinkProcess?> GetByConfirmationAsync(
            Guid creationConfirmationId,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Process?.CreationConfirmationId == creationConfirmationId
                ? this.Process
                : null);

        public Task AddAsync(
            ReservationGuestRecordLinkProcess value,
            CancellationToken cancellationToken)
        {
            Assert.Null(this.Process);
            this.Process = value;
            this.AddCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReservationRepository(Reservation reservation)
        : IReservationRepository
    {
        public Task AddAsync(
            Reservation value,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Reservation?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) => Task.FromResult(
            reservation.PropertyId == propertyId && reservation.Id == reservationId
                ? reservation
                : null);

        public Task<Reservation?> GetForDataRightsAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            this.GetAsync(propertyId, reservationId, cancellationToken);

        public Task<Reservation?> GetAsyncByReservationId(
            Guid reservationId,
            CancellationToken cancellationToken) => Task.FromResult(
            reservation.Id == reservationId ? reservation : null);

        public Task<Reservation?> GetByExternalSourceAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ExternalSourceExistsAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ReservationListResponse> ListAsync(
            Guid propertyId,
            IReadOnlyCollection<ReservationStatus>? statuses,
            string? search,
            ReservationListOrder order,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeGuestProjectionRepository(bool isLinkable)
        : IReservationGuestProfileProjectionRepository
    {
        public Task<bool> IsLinkableAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) => Task.FromResult(isLinkable);

        public Task ApplyAsync(
            ReservationGuestProfileProjectionWriteModel profile,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ApplyRestrictionAsync(
            ReservationGuestProcessingRestrictionProjectionWriteModel restriction,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeRestrictionGate(
        GuestProcessingRestrictionGateResult result)
        : IGuestProcessingRestrictionGate
    {
        public Task<GuestProcessingRestrictionGateResult> EvaluateAsync(
            GuestProcessingRestrictionGateRequest request,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock(DateTimeOffset nowUtc) : ISystemClock
    {
        public DateTimeOffset UtcNow => nowUtc;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
