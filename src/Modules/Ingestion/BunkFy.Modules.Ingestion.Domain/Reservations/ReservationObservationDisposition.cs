namespace BunkFy.Modules.Ingestion.Domain.Reservations;

public enum ReservationObservationDisposition
{
    Unknown = 0,
    Ready = 1,
    Deferred = 2,
    Replay = 3,
    Stale = 4,
    RequiresReview = 5
}
