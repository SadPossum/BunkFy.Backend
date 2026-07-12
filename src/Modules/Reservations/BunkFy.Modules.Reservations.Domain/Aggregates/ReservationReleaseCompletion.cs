namespace BunkFy.Modules.Reservations.Domain.Aggregates;

public enum ReservationReleaseCompletion
{
    Unknown = 0,
    Cancelled = 1,
    NoShow = 2,
    CheckedOut = 3
}
