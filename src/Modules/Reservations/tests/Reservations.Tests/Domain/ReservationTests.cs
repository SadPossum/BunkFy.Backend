namespace Reservations.Tests;

using Gma.Framework.Results;
using Reservations.Domain.Aggregates;
using Reservations.Domain.Errors;
using Reservations.Domain.Events;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationTests
{
    private static readonly Guid PropertyId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UnitId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid RequestId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_pending_and_raises_allocation_request_fact()
    {
        Reservation reservation = CreateReservation().Value;

        Assert.Equal(ReservationState.PendingAllocation, reservation.Status);
        Assert.Equal(1, reservation.Version);
        Assert.Equal(UnitId, Assert.Single(reservation.RequestedUnits).InventoryUnitId);
        ReservationCreatedDomainEvent domainEvent =
            Assert.IsType<ReservationCreatedDomainEvent>(Assert.Single(reservation.DomainEvents));
        Assert.Equal(RequestId, domainEvent.AllocationRequestId);
    }

    [Fact]
    public void Only_correlated_inventory_confirmation_can_confirm()
    {
        Reservation reservation = CreateReservation().Value;
        Guid allocationId = Guid.NewGuid();

        Assert.Equal(
            ReservationsDomainErrors.AllocationCorrelationMismatch,
            reservation.ConfirmAllocation(Guid.NewGuid(), allocationId, 1, Guid.NewGuid(), Now).Error);
        Assert.True(reservation.ConfirmAllocation(RequestId, allocationId, 1, Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(ReservationState.Confirmed, reservation.Status);
        Assert.Equal(allocationId, reservation.AllocationId);
        Assert.Equal(2, reservation.Version);
    }

    [Fact]
    public void Allocation_rejection_is_terminal_for_the_initial_attempt()
    {
        Reservation reservation = CreateReservation().Value;

        Assert.True(reservation.RejectAllocation(
            RequestId,
            ReservationAllocationRejection.AllocationConflict,
            Guid.NewGuid(),
            Now).IsSuccess);

        Assert.Equal(ReservationState.AllocationRejected, reservation.Status);
        Assert.Equal(ReservationAllocationRejection.AllocationConflict, reservation.AllocationRejection);
        Assert.Equal(
            ReservationsDomainErrors.InvalidTransition,
            reservation.ConfirmAllocation(RequestId, Guid.NewGuid(), 1, Guid.NewGuid(), Now).Error);
    }

    [Fact]
    public void Confirmed_cancellation_waits_for_correlated_release()
    {
        Reservation reservation = CreateReservation().Value;
        reservation.ConfirmAllocation(RequestId, Guid.NewGuid(), 1, Guid.NewGuid(), Now);
        reservation.ClearDomainEvents();
        Guid releaseRequestId = Guid.NewGuid();

        Assert.True(reservation.RequestCancellation(2, releaseRequestId, Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(ReservationState.CancellationPending, reservation.Status);
        Assert.IsType<ReservationCancellationRequestedDomainEvent>(Assert.Single(reservation.DomainEvents));
        Assert.Equal(
            ReservationsDomainErrors.AllocationCorrelationMismatch,
            reservation.CompleteCancellation(Guid.NewGuid(), Now).Error);
        Assert.True(reservation.CompleteCancellation(releaseRequestId, Now).IsSuccess);
        Assert.Equal(ReservationState.Cancelled, reservation.Status);
    }

    [Fact]
    public void Pending_cancellation_compensates_a_late_confirmation()
    {
        Reservation reservation = CreateReservation().Value;
        reservation.ClearDomainEvents();
        Guid releaseRequestId = Guid.NewGuid();

        Assert.True(reservation.RequestCancellation(1, releaseRequestId, Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(ReservationState.CancellationPending, reservation.Status);
        Assert.Empty(reservation.DomainEvents);

        Assert.True(reservation.ConfirmAllocation(
            RequestId,
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            Now).IsSuccess);

        ReservationCancellationRequestedDomainEvent release =
            Assert.IsType<ReservationCancellationRequestedDomainEvent>(Assert.Single(reservation.DomainEvents));
        Assert.Equal(releaseRequestId, release.ReleaseRequestId);
    }

    [Fact]
    public void Pending_cancellation_finishes_locally_when_allocation_is_rejected()
    {
        Reservation reservation = CreateReservation().Value;
        reservation.ClearDomainEvents();
        reservation.RequestCancellation(1, Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.True(reservation.RejectAllocation(
            RequestId,
            ReservationAllocationRejection.AllocationConflict,
            Guid.NewGuid(),
            Now).IsSuccess);

        Assert.Equal(ReservationState.Cancelled, reservation.Status);
        Assert.IsType<ReservationCancelledDomainEvent>(Assert.Single(reservation.DomainEvents));
    }

    [Fact]
    public void Create_validates_range_units_guest_and_external_source()
    {
        Assert.Equal(ReservationsDomainErrors.StayRangeInvalid, CreateReservation(departure: new DateOnly(2026, 8, 1)).Error);
        Assert.Equal(ReservationsDomainErrors.RequestedUnitsInvalid, CreateReservation(units: [UnitId, UnitId]).Error);
        Assert.Equal(ReservationsDomainErrors.PrimaryGuestNameInvalid, CreateReservation(primaryGuestName: " ").Error);
        Assert.Equal(ReservationsDomainErrors.GuestCountInvalid, CreateReservation(guestCount: 0).Error);
        Assert.Equal(
            ReservationsDomainErrors.SourceInvalid,
            CreateReservation(source: ReservationSource.External, sourceSystem: null, sourceReference: null).Error);
    }

    private static Result<Reservation> CreateReservation(
        DateOnly? departure = null,
        IReadOnlyCollection<Guid>? units = null,
        string primaryGuestName = "Ada Guest",
        int guestCount = 1,
        ReservationSource source = ReservationSource.Direct,
        string? sourceSystem = null,
        string? sourceReference = null) =>
        Reservation.Create(
            Guid.NewGuid(),
            "tenant-a",
            PropertyId,
            RequestId,
            new DateOnly(2026, 8, 1),
            departure ?? new DateOnly(2026, 8, 3),
            units ?? [UnitId],
            primaryGuestName,
            "ada@example.test",
            "+100000000",
            guestCount,
            source,
            sourceSystem,
            sourceReference,
            "Late arrival",
            Guid.NewGuid(),
            Now);
}
