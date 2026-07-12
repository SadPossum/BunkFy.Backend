namespace BunkFy.Modules.Ingestion.Domain.Reservations;

public enum ReservationSourceLinkState
{
    Unknown = 0,
    AwaitingCreate = 1,
    Linked = 2,
    CancellationPending = 3,
    Cancelled = 4
}
