namespace BunkFy.Modules.Retention.Admin.Contracts;

using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Administration;

public static class RetentionAdminPermissions
{
    public static readonly AdminPermission Read =
        AdminPermission.Create(RetentionPermissionCodes.Read);
    public static readonly AdminPermission Manage = AdminPermission.Create(RetentionPermissionCodes.Manage);
    public static readonly AdminPermission Retry =
        AdminPermission.Create(RetentionPermissionCodes.Retry);
}
