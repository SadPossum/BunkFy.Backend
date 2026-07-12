namespace BunkFy.Modules.Inventory.Contracts;

public enum InventoryAllocationReleaseRejectionReason
{
    Unknown = 0,
    AllocationNotFound = 1,
    ReservationMismatch = 2,
    VersionConflict = 3,
    AllocationNotActive = 4
}
