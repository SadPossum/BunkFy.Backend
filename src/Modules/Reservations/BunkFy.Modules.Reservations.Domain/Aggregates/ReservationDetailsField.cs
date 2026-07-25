namespace BunkFy.Modules.Reservations.Domain.Aggregates;

public enum ReservationDetailsField
{
    Unknown = 0,
    PrimaryGuestName = 1,
    Email = 2,
    Phone = 3,
    GuestCount = 4,
    Notes = 5,
    ExpectedArrivalTime = 6,
    ExpectedDepartureTime = 7
}
