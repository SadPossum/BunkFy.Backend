namespace BunkFy.Modules.Workspaces.Contracts;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Modules.AccessControl.Contracts;

public static class WorkspaceAccessRoles
{
    public const string Owner = "bunkfy-workspace-owner";
    public const string Member = "bunkfy-workspace-member";

    public static IReadOnlyList<string> OwnerPermissions { get; } =
        [AccessControlPermissionGrants.OwnerWildcard];

    public static IReadOnlyList<string> MemberPermissions { get; } =
    [
        PropertiesAdminPermissionCodes.Read,
        InventoryAdminPermissionCodes.Read,
        InventoryAdminPermissionCodes.BlocksManage,
        ReservationsAdminPermissionCodes.Read,
        ReservationsAdminPermissionCodes.Create,
        ReservationsAdminPermissionCodes.Manage,
        ReservationsAdminPermissionCodes.ManageGuests,
        ReservationsAdminPermissionCodes.Cancel,
        ReservationsAdminPermissionCodes.CheckIn,
        ReservationsAdminPermissionCodes.NoShow,
        ReservationsAdminPermissionCodes.CheckOut,
        GuestsAdminPermissionCodes.Read,
        GuestsAdminPermissionCodes.Create,
        GuestsAdminPermissionCodes.Manage,
        StaffAdminPermissionCodes.Read
    ];

    public static IReadOnlyList<string> DelegablePermissions { get; } = MemberPermissions;
}
