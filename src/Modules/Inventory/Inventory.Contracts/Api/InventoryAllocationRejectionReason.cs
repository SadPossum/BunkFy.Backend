namespace Inventory.Contracts;

public enum InventoryAllocationRejectionReason
{
    Unknown = 0,
    UnitNotFound = 1,
    UnitInactive = 2,
    UnitNotSellable = 3,
    ManualBlockConflict = 4,
    AllocationConflict = 5,
    ExistingActiveAllocation = 6,
    RequestMismatch = 7
}
