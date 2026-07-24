namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class LinkReservationGuestRestrictionTests
{
    [Theory]
    [InlineData(GuestProcessingRestrictionDecision.Unknown)]
    [InlineData(GuestProcessingRestrictionDecision.Restricted)]
    [InlineData(GuestProcessingRestrictionDecision.UnsupportedContractVersion)]
    public async Task Authoritative_restriction_denial_prevents_new_guest_link(
        GuestProcessingRestrictionDecision decision)
    {
        Reservation reservation = CreateReservation();
        Guid guestId = Guid.NewGuid();
        RecordingRestrictionGate gate = new(ResultFor(decision));
        LinkReservationGuestCommandHandler handler = CreateHandler(
            reservation,
            localLinkable: true,
            gate);

        Result<ReservationDto> result = await handler.HandleAsync(
            Command(reservation, guestId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Reservations.GuestNotLinkable", result.Error.Code);
        Assert.Empty(reservation.Guests);
        Assert.Equal(1, gate.CallCount);
    }

    [Fact]
    public async Task Local_denial_short_circuits_the_authoritative_gate()
    {
        Reservation reservation = CreateReservation();
        RecordingRestrictionGate gate = new(
            GuestProcessingRestrictionGateResult.Allowed(
                GuestProcessingRestrictionContract.CurrentVersion,
                projectionRevision: 1));
        LinkReservationGuestCommandHandler handler = CreateHandler(
            reservation,
            localLinkable: false,
            gate);

        Result<ReservationDto> result = await handler.HandleAsync(
            Command(reservation, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Reservations.GuestNotLinkable", result.Error.Code);
        Assert.Equal(0, gate.CallCount);
    }

    [Fact]
    public async Task Current_unrestricted_owner_state_allows_a_new_guest_link()
    {
        Reservation reservation = CreateReservation();
        Guid guestId = Guid.NewGuid();
        RecordingRestrictionGate gate = new(
            GuestProcessingRestrictionGateResult.Allowed(
                GuestProcessingRestrictionContract.CurrentVersion,
                projectionRevision: 3));
        LinkReservationGuestCommandHandler handler = CreateHandler(
            reservation,
            localLinkable: true,
            gate);

        Result<ReservationDto> result = await handler.HandleAsync(
            Command(reservation, guestId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.Guests, guest => guest.GuestId == guestId);
        Assert.Equal(1, gate.CallCount);
    }

    [Fact]
    public async Task Idempotent_existing_link_replay_does_not_require_a_new_processing_decision()
    {
        Reservation reservation = CreateReservation();
        Guid guestId = Guid.NewGuid();
        Assert.True(reservation.LinkGuest(
            guestId,
            ReservationGuestRole.Primary,
            replaceExistingRole: false,
            reservation.Version,
            "user:staff",
            Guid.NewGuid(),
            Now).IsSuccess);
        reservation.ClearDomainEvents();
        RecordingRestrictionGate gate = new(GuestProcessingRestrictionGateResult.Unknown);
        LinkReservationGuestCommandHandler handler = CreateHandler(
            reservation,
            localLinkable: false,
            gate);

        Result<ReservationDto> result = await handler.HandleAsync(
            Command(reservation, guestId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, gate.CallCount);
        Assert.Empty(reservation.DomainEvents);
    }

    private static LinkReservationGuestCommandHandler CreateHandler(
        Reservation reservation,
        bool localLinkable,
        IGuestProcessingRestrictionGate gate) => new(
        new FakeReservationRepository(reservation),
        new FakeGuestProjectionRepository(localLinkable),
        gate,
        new TestReservationCountryPolicyAdmission(),
        new TestScopeContext(),
        new TestClock(),
        new TestIdGenerator());

    private static LinkReservationGuestCommand Command(Reservation reservation, Guid guestId) => new(
        reservation.PropertyId,
        reservation.Id,
        guestId,
        ReservationGuestRoleKind.Primary,
        ReplaceExistingRole: false,
        reservation.Version,
        "user:staff");

    private static GuestProcessingRestrictionGateResult ResultFor(
        GuestProcessingRestrictionDecision decision) => decision switch
        {
            GuestProcessingRestrictionDecision.Allowed =>
                GuestProcessingRestrictionGateResult.Allowed(
                    GuestProcessingRestrictionContract.CurrentVersion,
                    projectionRevision: 1),
            GuestProcessingRestrictionDecision.Restricted =>
                GuestProcessingRestrictionGateResult.Restricted(
                    GuestProcessingRestrictionContract.CurrentVersion,
                    projectionRevision: 1),
            GuestProcessingRestrictionDecision.UnsupportedContractVersion =>
                GuestProcessingRestrictionGateResult.Unsupported(
                    GuestProcessingRestrictionContract.CurrentVersion + 1,
                    projectionRevision: 1),
            _ => GuestProcessingRestrictionGateResult.Unknown
        };

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
        Now).Value;

    private sealed class FakeReservationRepository(Reservation reservation) : IReservationRepository
    {
        public Task AddAsync(Reservation value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Reservation?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) => Task.FromResult(
            reservation.PropertyId == propertyId && reservation.Id == reservationId
                ? reservation
                : null);

        public Task<Reservation?> GetAsyncByReservationId(
            Guid reservationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

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

    private sealed class RecordingRestrictionGate(GuestProcessingRestrictionGateResult result)
        : IGuestProcessingRestrictionGate
    {
        public int CallCount { get; private set; }

        public Task<GuestProcessingRestrictionGateResult> EvaluateAsync(
            GuestProcessingRestrictionGateRequest request,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            return Task.FromResult(result);
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
        public Guid NewId() => Guid.NewGuid();
    }

    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 18, 0, 0, TimeSpan.Zero);
}
