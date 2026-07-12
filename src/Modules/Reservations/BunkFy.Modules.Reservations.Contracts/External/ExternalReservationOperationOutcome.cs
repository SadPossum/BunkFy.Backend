namespace BunkFy.Modules.Reservations.Contracts;

public enum ExternalReservationOperationOutcome
{
    Unknown = 0,
    Applied = 1,
    Accepted = 2,
    Unchanged = 3,
    ReservationNotFound = 4,
    DetailsRevisionConflict = 5,
    SourceAlreadyExists = 6,
    ValidationRejected = 7,
    InvalidTransition = 8,
    OperationConflict = 9
}
