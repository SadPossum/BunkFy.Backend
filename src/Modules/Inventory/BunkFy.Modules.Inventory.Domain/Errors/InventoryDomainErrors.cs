namespace BunkFy.Modules.Inventory.Domain.Errors;

using Gma.Framework.Results;

public static class InventoryDomainErrors
{
    public static readonly Error PropertyIdRequired = new("Inventory.PropertyIdRequired", "Property id is required.");
    public static readonly Error RoomIdRequired = new("Inventory.RoomIdRequired", "Room id is required.");
    public static readonly Error InventoryUnitIdRequired = new("Inventory.InventoryUnitIdRequired", "Inventory unit id is required.");
    public static readonly Error BlockIdRequired = new("Inventory.BlockIdRequired", "Block id is required.");
    public static readonly Error BlockGroupIdRequired = new("Inventory.BlockGroupIdRequired", "Block group id is required.");
    public static readonly Error RoomNotFound = new("Inventory.RoomNotFound", "The inventory room was not found.");
    public static readonly Error RoomRetired = new("Inventory.RoomRetired", "Retired room topology cannot be configured.");
    public static readonly Error BedLevelRequiresBeds = new("Inventory.BedLevelRequiresBeds", "Bed-level sales require at least one active bed.");
    public static readonly Error SalesModeInvalid = new("Inventory.SalesModeInvalid", "Sales mode must be room-level or bed-level.");
    public static readonly Error VersionConflict = new("Inventory.VersionConflict", "The inventory configuration has changed. Refresh and retry.");
    public static readonly Error StayRangeInvalid = new("Inventory.StayRangeInvalid", "Arrival must be before departure.");
    public static readonly Error BlockReasonInvalid = new("Inventory.BlockReasonInvalid", "Block reason is required and must be 500 characters or fewer.");
    public static readonly Error BlockAlreadyReleased = new("Inventory.BlockAlreadyReleased", "The inventory block is already released.");
    public static readonly Error AllocationIdRequired = new("Inventory.AllocationIdRequired", "Allocation id is required.");
    public static readonly Error ReservationIdRequired = new("Inventory.ReservationIdRequired", "Reservation id is required.");
    public static readonly Error AllocationRequestIdRequired = new("Inventory.AllocationRequestIdRequired", "Allocation request id is required.");
    public static readonly Error AllocationAmendmentRequestIdRequired = new("Inventory.AllocationAmendmentRequestIdRequired", "Allocation amendment request id is required.");
    public static readonly Error AllocationReleaseRequestIdRequired = new("Inventory.AllocationReleaseRequestIdRequired", "Allocation release request id is required.");
    public static readonly Error AllocationUnitsInvalid = new("Inventory.AllocationUnitsInvalid", "Allocation units must contain unique, non-empty ids within the supported limit.");
    public static readonly Error AllocationRejectionRequired = new("Inventory.AllocationRejectionRequired", "A rejected allocation requires a rejection reason.");
    public static readonly Error AllocationNotActive = new("Inventory.AllocationNotActive", "The inventory allocation is not active.");
}
