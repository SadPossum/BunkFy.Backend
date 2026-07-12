namespace BunkFy.Modules.Reservations.Contracts;

public enum ExternalReservationOperationKind
{
    Unknown = 0,
    Create = 1,
    ChangeGuestDetails = 2,
    Cancel = 3,
    Amend = 4
}
