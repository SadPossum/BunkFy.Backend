namespace BunkFy.Modules.Reservations.Domain.Aggregates;

public enum ReservationDetailsChangeOrigin
{
    Unknown = 0,
    Staff = 1,
    Adapter = 2,
    Admin = 3,
    System = 4
}
