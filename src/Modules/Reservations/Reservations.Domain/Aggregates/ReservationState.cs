namespace Reservations.Domain.Aggregates;

public enum ReservationState
{
    PendingAllocation = 1,
    Confirmed = 2,
    AllocationRejected = 3,
    CancellationPending = 4,
    Cancelled = 5
}
