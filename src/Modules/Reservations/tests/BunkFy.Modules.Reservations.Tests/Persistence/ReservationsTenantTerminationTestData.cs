namespace BunkFy.Modules.Reservations.Tests.Persistence;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal static class ReservationsTenantTerminationTestData
{
    public const string TenantId =
        "10000000-0000-0000-0000-000000000001";

    public static readonly DateTimeOffset Now =
        new(2026, 7, 31, 18, 0, 0, TimeSpan.Zero);

    public static Reservation CreateReservation(
        Guid? propertyId = null,
        string primaryGuestName = "Maya Chen") =>
        Reservation.Create(
            Guid.NewGuid(),
            TenantId,
            propertyId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            primaryGuestName,
            "maya@example.test",
            "+44 20 1234 5678",
            guestCount: 1,
            ReservationSource.Direct,
            sourceSystem: null,
            sourceReference: null,
            notes: "Late arrival",
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationDetailsChangeOrigin.Staff,
            initialDetailsActorId: "user:owner",
            initialAdapterConnectionId: null,
            initialExternalOperationId: null,
            Guid.NewGuid(),
            Now,
            expectedArrivalTime: new TimeOnly(15, 0),
            expectedDepartureTime: new TimeOnly(11, 0)).Value;
}
