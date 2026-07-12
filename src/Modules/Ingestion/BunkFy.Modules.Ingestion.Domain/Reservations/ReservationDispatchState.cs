namespace BunkFy.Modules.Ingestion.Domain.Reservations;

public enum ReservationDispatchState
{
    Unknown = 0,
    Pending = 1,
    Accepted = 2,
    Applied = 3,
    Unchanged = 4,
    ProposalRequired = 5,
    Rejected = 6,
    Conflict = 7
}
