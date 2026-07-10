namespace Inventory.Domain.Aggregates;

public enum InventoryAllocationRejection
{
    None = 0,
    UnitNotFound = 1,
    UnitInactive = 2,
    UnitNotSellable = 3,
    ManualBlockConflict = 4,
    AllocationConflict = 5,
    ExistingActiveAllocation = 6
}
