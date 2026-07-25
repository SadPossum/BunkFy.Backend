namespace BunkFy.Modules.Reservations.Tests.Domain;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationDataRightsCorrectionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Correction_requires_both_selected_revisions_and_reports_typed_fields()
    {
        Reservation reservation = CreateReservation();
        long initialVersion = reservation.Version;
        long initialDetailsRevision = reservation.DetailsRevision;

        var staleRecord = reservation.CorrectGuestDetails(
            "Corrected Guest",
            reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            reservation.Notes,
            expectedRecordVersion: reservation.Version + 1,
            expectedDetailsRevision: reservation.DetailsRevision,
            "user:privacy-operator",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now,
            reservation.ExpectedArrivalTime,
            reservation.ExpectedDepartureTime);
        var staleDetails = reservation.CorrectGuestDetails(
            "Corrected Guest",
            reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            reservation.Notes,
            expectedRecordVersion: reservation.Version,
            expectedDetailsRevision: reservation.DetailsRevision + 1,
            "user:privacy-operator",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now,
            reservation.ExpectedArrivalTime,
            reservation.ExpectedDepartureTime);
        Guid detailsEventId = Guid.NewGuid();
        var corrected = reservation.CorrectGuestDetails(
            "Corrected Guest",
            "corrected@example.test",
            reservation.Phone,
            reservation.GuestCount,
            reservation.Notes,
            reservation.Version,
            reservation.DetailsRevision,
            "user:privacy-operator",
            Guid.NewGuid(),
            detailsEventId,
            Now,
            reservation.ExpectedArrivalTime,
            reservation.ExpectedDepartureTime);

        Assert.Equal(ReservationsDomainErrors.VersionConflict, staleRecord.Error);
        Assert.Equal(ReservationsDomainErrors.DetailsRevisionConflict, staleDetails.Error);
        Assert.True(corrected.IsSuccess, corrected.Error.Code);
        Assert.Equal(
            [ReservationDetailsField.PrimaryGuestName, ReservationDetailsField.Email],
            corrected.Value.ChangedFields);
        Assert.Equal(initialVersion, corrected.Value.PreviousRecordVersion);
        Assert.Equal(initialVersion + 1, corrected.Value.CurrentRecordVersion);
        Assert.Equal(initialDetailsRevision, corrected.Value.PreviousDetailsRevision);
        Assert.Equal(initialDetailsRevision + 1, corrected.Value.CurrentDetailsRevision);
        Assert.Equal(detailsEventId, corrected.Value.DetailsChangeEventId);
        ReservationDetailsChangedDomainEvent changed =
            Assert.IsType<ReservationDetailsChangedDomainEvent>(
                reservation.DomainEvents.Last());
        Assert.Equal(ReservationDetailsChangeOrigin.DataRightsCorrection, changed.Origin);
    }

    [Fact]
    public void Correction_fails_closed_when_no_field_changes()
    {
        Reservation reservation = CreateReservation();
        long initialVersion = reservation.Version;
        long initialDetailsRevision = reservation.DetailsRevision;

        var result = reservation.CorrectGuestDetails(
            reservation.PrimaryGuestName,
            reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            reservation.Notes,
            reservation.Version,
            reservation.DetailsRevision,
            "user:privacy-operator",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now,
            reservation.ExpectedArrivalTime,
            reservation.ExpectedDepartureTime);

        Assert.Equal(ReservationsDomainErrors.DataRightsCorrectionNoChanges, result.Error);
        Assert.Equal(initialVersion, reservation.Version);
        Assert.Equal(initialDetailsRevision, reservation.DetailsRevision);
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
}
