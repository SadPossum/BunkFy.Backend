namespace BunkFy.Modules.Staff.Admin.Contracts;

using Gma.Framework.Administration;
using BunkFy.Modules.Staff.Contracts;

public static class StaffAdminPermissions
{
    public static readonly AdminPermission Read = AdminPermission.Create(StaffAdminPermissionCodes.Read);
    public static readonly AdminPermission Create = AdminPermission.Create(StaffAdminPermissionCodes.Create);
    public static readonly AdminPermission Manage = AdminPermission.Create(StaffAdminPermissionCodes.Manage);
    public static readonly AdminPermission AssignProperties = AdminPermission.Create(StaffAdminPermissionCodes.AssignProperties);
    public static readonly AdminPermission ManageLifecycle = AdminPermission.Create(StaffAdminPermissionCodes.ManageLifecycle);
}
