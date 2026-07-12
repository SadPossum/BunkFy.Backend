namespace BunkFy.Modules.Guests.Admin.Contracts;

using Gma.Framework.Administration;
using BunkFy.Modules.Guests.Contracts;

public static class GuestsAdminPermissions
{
    public static readonly AdminPermission Read = AdminPermission.Create(GuestsAdminPermissionCodes.Read);
    public static readonly AdminPermission Create = AdminPermission.Create(GuestsAdminPermissionCodes.Create);
    public static readonly AdminPermission Manage = AdminPermission.Create(GuestsAdminPermissionCodes.Manage);
    public static readonly AdminPermission Archive = AdminPermission.Create(GuestsAdminPermissionCodes.Archive);
}
