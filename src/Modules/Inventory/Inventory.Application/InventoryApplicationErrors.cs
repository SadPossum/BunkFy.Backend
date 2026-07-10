namespace Inventory.Application;

using Gma.Framework.Results;
using Inventory.Domain.Errors;

public static class InventoryApplicationErrors
{
    public static readonly Error AccessDenied = new("Inventory.AccessDenied", "The subject cannot access the requested inventory scope.");
    public static readonly Error TenantRequired = new("Inventory.TenantRequired", "A tenant context is required.");
    public static readonly Error PropertyNotFound = new("Inventory.PropertyNotFound", "The inventory property was not found.");
    public static Error RoomNotFound => InventoryDomainErrors.RoomNotFound;
    public static Error RoomRetired => InventoryDomainErrors.RoomRetired;
    public static Error BedLevelRequiresBeds => InventoryDomainErrors.BedLevelRequiresBeds;
    public static Error SalesModeInvalid => InventoryDomainErrors.SalesModeInvalid;
    public static Error VersionConflict => InventoryDomainErrors.VersionConflict;
    public static readonly Error InventoryUnitNotFound = new("Inventory.InventoryUnitNotFound", "The inventory unit was not found.");
    public static readonly Error InventoryUnitInactive = new("Inventory.InventoryUnitInactive", "The inventory unit topology is inactive.");
    public static readonly Error InventoryUnitNotSellable = new("Inventory.InventoryUnitNotSellable", "The inventory unit is not sellable in the room's current mode.");
    public static readonly Error BlockNotFound = new("Inventory.BlockNotFound", "The inventory block was not found.");
    public static readonly Error BlockOverlap = new("Inventory.BlockOverlap", "An active manual block already overlaps this stay range.");
    public static readonly Error BlockAllocationConflict = new("Inventory.BlockAllocationConflict", "An active allocation overlaps this stay range.");
    public static Error StayRangeInvalid => InventoryDomainErrors.StayRangeInvalid;
    public static Error BlockReasonInvalid => InventoryDomainErrors.BlockReasonInvalid;
    public static Error BlockAlreadyReleased => InventoryDomainErrors.BlockAlreadyReleased;
}
