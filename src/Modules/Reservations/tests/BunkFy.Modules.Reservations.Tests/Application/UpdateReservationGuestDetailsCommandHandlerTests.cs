namespace BunkFy.Modules.Reservations.Tests;

using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class UpdateReservationGuestDetailsCommandHandlerTests
{
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
        FakeReservationRepository repository = new(reservation);
        UpdateReservationGuestDetailsCommandHandler handler = new(
            repository,
            new TestClock(),
            new TestIdGenerator());

        Result<ReservationDto> result = await handler.HandleAsync(
            new UpdateReservationGuestDetailsCommand(
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
        Assert.Equal(ReservationDetailsChangeOriginKind.Staff, result.Value.LastDetailsChangeOrigin);
        Assert.Equal(3, result.Value.Version);
    }

    [Fact]
    public async Task Management_command_cannot_impersonate_adapter_origin()
    {
        Reservation reservation = CreateReservation();
        UpdateReservationGuestDetailsCommandHandler handler = new(
            new FakeReservationRepository(reservation),
            new TestClock(),
            new TestIdGenerator());

        Result<ReservationDto> result = await handler.HandleAsync(
            new UpdateReservationGuestDetailsCommand(
                reservation.PropertyId,
                reservation.Id,
                reservation.PrimaryGuestName,
                reservation.Email,
                reservation.Phone,
                reservation.GuestCount,
                reservation.Notes,
                ExpectedDetailsRevision: 1,
                ReservationDetailsChangeOriginKind.Adapter,
                "service:adapter"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Reservations.DetailsChangeProvenanceInvalid", result.Error.Code);
    }

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
            ReservationStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ReservationListResponse([], pageRequest.Page, pageRequest.PageSize));
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
