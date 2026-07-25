namespace BunkFy.Modules.Reservations.Contracts;

using System.Collections.Frozen;

public static class ReservationDataRightsFieldKeys
{
    public const string PrimaryGuestName = "reservation.guest.primary-name";
    public const string Email = "reservation.guest.email";
    public const string Phone = "reservation.guest.phone";
    public const string GuestCount = "reservation.guest.count";
    public const string Notes = "reservation.guest.notes";
    public const string ExpectedArrivalTime = "reservation.stay.expected-arrival-time";
    public const string ExpectedDepartureTime = "reservation.stay.expected-departure-time";

    public static IReadOnlySet<string> All { get; } = new[]
    {
        PrimaryGuestName,
        Email,
        Phone,
        GuestCount,
        Notes,
        ExpectedArrivalTime,
        ExpectedDepartureTime
    }.ToFrozenSet(StringComparer.Ordinal);
}
