namespace BunkFy.Modules.Inventory.Application;

using Gma.Framework.Results;
using BunkFy.Modules.Inventory.Domain.Errors;

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
    public static readonly Error RoomHasActiveClaims = new(
        "Inventory.RoomHasActiveClaims",
        "The room sales mode cannot change while active reservations or inventory blocks depend on it.");
    public static readonly Error InventoryUnitNotFound = new("Inventory.InventoryUnitNotFound", "The inventory unit was not found.");
    public static readonly Error InventoryUnitInactive = new("Inventory.InventoryUnitInactive", "The inventory unit topology is inactive.");
    public static readonly Error InventoryUnitNotSellable = new("Inventory.InventoryUnitNotSellable", "The inventory unit is not sellable in the room's current mode.");
    public static readonly Error BlockNotFound = new("Inventory.BlockNotFound", "The inventory block was not found.");
    public static readonly Error BlockGroupNotFound = new("Inventory.BlockGroupNotFound", "The inventory block group was not found.");
    public static readonly Error BlockTargetInvalid = new("Inventory.BlockTargetInvalid", "The inventory block target is invalid.");
    public static readonly Error BlockTargetEmpty = new("Inventory.BlockTargetEmpty", "The selected target has no sellable inventory.");
    public static readonly Error BlockOverlap = new("Inventory.BlockOverlap", "An active manual block already overlaps this stay range.");
    public static readonly Error BlockAllocationConflict = new("Inventory.BlockAllocationConflict", "An active allocation overlaps this stay range.");
    public static Error StayRangeInvalid => InventoryDomainErrors.StayRangeInvalid;
    public static Error BlockReasonInvalid => InventoryDomainErrors.BlockReasonInvalid;
    public static Error BlockAlreadyReleased => InventoryDomainErrors.BlockAlreadyReleased;
    public static readonly Error BedRetirementNotFound = new("Inventory.BedRetirementNotFound", "The bed retirement process was not found.");
    public static readonly Error BedRetirementRetryInvalid = new("Inventory.BedRetirementRetryInvalid", "Only a rejected bed retirement can be retried.");
    public static readonly Error BedRetirementStillDraining = new("Inventory.BedRetirementStillDraining", "Active reservations or inventory blocks still depend on this bed.");
    public static readonly Error BedRetirementInProgress = new("Inventory.BedRetirementInProgress", "A bed retirement must finish before the room can be retired.");
    public static readonly Error RoomRetirementNotFound = new("Inventory.RoomRetirementNotFound", "The room retirement process was not found.");
    public static readonly Error RoomRetirementRetryInvalid = new("Inventory.RoomRetirementRetryInvalid", "Only a rejected room retirement can be retried.");
    public static readonly Error RoomRetirementStillDraining = new("Inventory.RoomRetirementStillDraining", "Active reservations, inventory blocks, or bed retirements still depend on this room.");
    public static readonly Error RoomRetirementInProgress = new("Inventory.RoomRetirementInProgress", "The room is already being retired, so its beds cannot be retired independently.");
}
