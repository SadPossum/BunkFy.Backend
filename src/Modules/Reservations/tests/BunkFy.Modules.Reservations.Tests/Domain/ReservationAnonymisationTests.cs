namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Errors;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationAnonymisationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Terminal_reservation_is_redacted_without_changing_operational_facts()
    {
        Reservation reservation = CreateReservation();
        Guid guestId = Guid.NewGuid();
        Assert.True(reservation.LinkGuest(
            guestId,
            ReservationGuestRole.Primary,
            replaceExistingRole: false,
            reservation.Version,
            "user:front-desk",
            Guid.NewGuid(),
            Now.AddHours(-2)).IsSuccess);
        Assert.True(reservation.RejectAllocation(
            reservation.AllocationRequestId,
            ReservationAllocationRejection.UnitNotSellable,
            Guid.NewGuid(),
            Now.AddHours(-1)).IsSuccess);
        reservation.ClearDomainEvents();
        long selectedVersion = reservation.Version;
        long selectedDetailsRevision = reservation.DetailsRevision;
        DateOnly arrival = reservation.Arrival;
        DateOnly departure = reservation.Departure;
        Guid[] units = reservation.RequestedUnits
            .Select(unit => unit.InventoryUnitId)
            .ToArray();

        Result<ReservationAnonymisationOutcome> result = reservation.Anonymise(
            selectedVersion,
            selectedDetailsRevision,
            "user:privacy-executor",
            Guid.NewGuid(),
            Now);

        Assert.True(result.IsSuccess);
        Assert.True(reservation.IsAnonymised);
        Assert.Equal(Now, reservation.AnonymisedAtUtc);
        Assert.Equal(Reservation.AnonymisedGuestName, reservation.PrimaryGuestName);
        Assert.Equal(
            Reservation.AnonymisedGuestName.ToUpperInvariant(),
            reservation.PrimaryGuestNameSearch);
        Assert.Null(reservation.Email);
        Assert.Null(reservation.EmailSearch);
        Assert.Null(reservation.Phone);
        Assert.Null(reservation.PhoneSearch);
        Assert.Null(reservation.SourceReference);
        Assert.Null(reservation.Notes);
        Assert.Empty(reservation.Guests);
        Assert.Equal(ReservationState.AllocationRejected, reservation.Status);
        Assert.Equal(arrival, reservation.Arrival);
        Assert.Equal(departure, reservation.Departure);
        Assert.Equal(units, reservation.RequestedUnits
            .Select(unit => unit.InventoryUnitId));
        Assert.Equal(selectedVersion + 1, reservation.Version);
        Assert.Equal(selectedDetailsRevision + 1, reservation.DetailsRevision);
        Assert.Equal(
            ReservationDetailsChangeOrigin.DataRightsAnonymisation,
            reservation.LastDetailsChangeOrigin);
        Assert.Equal(1, result.Value.RemovedGuestLinkCount);
        Assert.Contains(nameof(Reservation.IsAnonymised), result.Value.ChangedFields);
        Assert.Contains(nameof(Reservation.PrimaryGuestName), result.Value.ChangedFields);
        Assert.Contains(nameof(Reservation.Email), result.Value.ChangedFields);
        Assert.Contains(nameof(Reservation.Phone), result.Value.ChangedFields);
        Assert.Contains(nameof(Reservation.SourceReference), result.Value.ChangedFields);
        Assert.Contains(nameof(Reservation.Notes), result.Value.ChangedFields);
        Assert.Contains(nameof(Reservation.Guests), result.Value.ChangedFields);
        Assert.True(reservation.MatchesAnonymisedState(
            reservation.Version,
            reservation.DetailsRevision,
            Now));
        Assert.Empty(reservation.DomainEvents);
    }

    [Fact]
    public void Active_stale_and_repeated_anonymisation_fail_closed()
    {
        Reservation active = CreateReservation();
        Assert.Equal(
            ReservationsDomainErrors.ReservationNotEligibleForAnonymisation,
            active.Anonymise(
                active.Version,
                active.DetailsRevision,
                "user:privacy",
                Guid.NewGuid(),
                Now).Error);

        Reservation terminal = CreateReservation();
        Assert.True(terminal.RejectAllocation(
            terminal.AllocationRequestId,
            ReservationAllocationRejection.UnitNotSellable,
            Guid.NewGuid(),
            Now.AddMinutes(-1)).IsSuccess);
        Assert.Equal(
            ReservationsDomainErrors.VersionConflict,
            terminal.Anonymise(
                terminal.Version + 1,
                terminal.DetailsRevision,
                "user:privacy",
                Guid.NewGuid(),
                Now).Error);
        Assert.True(terminal.Anonymise(
            terminal.Version,
            terminal.DetailsRevision,
            "user:privacy",
            Guid.NewGuid(),
            Now).IsSuccess);
        Assert.Equal(
            ReservationsDomainErrors.ReservationAlreadyAnonymised,
            terminal.Anonymise(
                terminal.Version,
                terminal.DetailsRevision,
                "user:privacy",
                Guid.NewGuid(),
                Now.AddMinutes(1)).Error);
        Assert.Equal(
            ReservationsDomainErrors.ReservationAlreadyAnonymised,
            terminal.UpdateGuestDetails(
                "Restored Guest",
                "restored@example.test",
                null,
                1,
                null,
                terminal.DetailsRevision,
                ReservationDetailsChangeOrigin.DataRightsCorrection,
                "user:privacy",
                null,
                null,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now.AddMinutes(1)).Error);
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
        ReservationSource.External,
        sourceSystem: "booking.com",
        sourceReference: "direct-reference",
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
}
