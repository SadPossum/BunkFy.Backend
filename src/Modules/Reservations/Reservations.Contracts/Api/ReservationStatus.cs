namespace Reservations.Contracts;

public enum ReservationStatus
{
    Unknown = 0,
    PendingAllocation = 1,
    Confirmed = 2,
    AllocationRejected = 3,
    CancellationPending = 4,
    Cancelled = 5
}
