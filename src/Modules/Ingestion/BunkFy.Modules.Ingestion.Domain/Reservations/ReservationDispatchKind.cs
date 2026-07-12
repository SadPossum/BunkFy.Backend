namespace BunkFy.Modules.Ingestion.Domain.Reservations;

public enum ReservationDispatchKind
{
    Unknown = 0,
    Create = 1,
    ChangeGuestDetails = 2,
    Cancel = 3,
    Amend = 4
}
