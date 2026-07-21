namespace BunkFy.Modules.Workspaces.Contracts;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Modules.AccessControl.Contracts;

public static class WorkspaceAccessRoles
{
    public const string Owner = "bunkfy-workspace-owner";
    public const string MembershipMarker = "bunkfy-workspace-member-v2";
    public const string LegacyMember = "bunkfy-workspace-member";

    public static IReadOnlyList<string> OwnerPermissions { get; } =
        [AccessControlPermissionGrants.OwnerWildcard];

    public static IReadOnlyList<string> MembershipMarkerPermissions { get; } = [];

    public static IReadOnlyList<string> LegacyMemberPermissions { get; } =
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

    public static IReadOnlyList<string> DelegablePermissions { get; } =
    [
        PropertiesAdminPermissionCodes.Read,
        PropertiesAdminPermissionCodes.PropertiesManage,
        PropertiesAdminPermissionCodes.RoomsManage,
        PropertiesAdminPermissionCodes.BedsManage,
        InventoryAdminPermissionCodes.Read,
        InventoryAdminPermissionCodes.Configure,
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
        GuestsAdminPermissionCodes.Archive,
        StaffAdminPermissionCodes.Read,
        StaffAdminPermissionCodes.SensitiveProfileRead,
        StaffAdminPermissionCodes.Create,
        StaffAdminPermissionCodes.Manage,
        StaffAdminPermissionCodes.AssignProperties,
        StaffAdminPermissionCodes.ManageLifecycle,
        IngestionAdminPermissionCodes.Read,
        IngestionAdminPermissionCodes.ConnectionsManage,
        IngestionAdminPermissionCodes.CredentialsManage,
        IngestionAdminPermissionCodes.RunsManage,
        IngestionAdminPermissionCodes.RawPayloadsRead,
        IngestionAdminPermissionCodes.SensitiveHistoryRead,
        IngestionAdminPermissionCodes.RetentionManage,
        IngestionAdminPermissionCodes.ReprocessingManage,
        IngestionAdminPermissionCodes.LegalHoldsManage,
        IngestionAdminPermissionCodes.ProposalsDecide
    ];
}
