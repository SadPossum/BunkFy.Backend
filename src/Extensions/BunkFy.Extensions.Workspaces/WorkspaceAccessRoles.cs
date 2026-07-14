namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using Gma.Modules.AccessControl.Application;

public static class WorkspaceAccessRoles
{
    public const string Owner = "bunkfy-workspace-owner";
    public const string Member = "bunkfy-workspace-member";

    public static IReadOnlyList<string> OwnerPermissions { get; } =
        [AccessControlPermissionGrant.OwnerWildcard];

    public static IReadOnlyList<string> MemberPermissions { get; } =
    [
        PropertiesAdminPermissionCodes.Read,
        InventoryAdminPermissionCodes.Read
    ];
}
