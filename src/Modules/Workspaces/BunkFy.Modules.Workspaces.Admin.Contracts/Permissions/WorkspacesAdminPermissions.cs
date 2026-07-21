namespace BunkFy.Modules.Workspaces.Admin.Contracts;

using Gma.Framework.Administration;

public static class WorkspacesAdminPermissions
{
    public static readonly AdminPermission AccessBootstrap =
        AdminPermission.Create("workspaces.access.bootstrap");
}
