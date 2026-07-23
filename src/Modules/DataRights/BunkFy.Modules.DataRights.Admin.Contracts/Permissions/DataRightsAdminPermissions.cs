namespace BunkFy.Modules.DataRights.Admin.Contracts;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Administration;

public static class DataRightsAdminPermissions
{
    public static readonly AdminPermission Manage = AdminPermission.Create(DataRightsAdminPermissionCodes.Manage);
}
