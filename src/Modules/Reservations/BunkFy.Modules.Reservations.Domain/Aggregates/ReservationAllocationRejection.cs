namespace BunkFy.Modules.Reservations.Domain.Aggregates;

public enum ReservationAllocationRejection
{
    UnitNotFound = 1,
    UnitInactive = 2,
    UnitNotSellable = 3,
    ManualBlockConflict = 4,
    AllocationConflict = 5,
    ExistingActiveAllocation = 6,
    RequestMismatch = 7
}
