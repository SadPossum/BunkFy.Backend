namespace Reservations.Admin.Contracts;

using Reservations.Contracts;
using Gma.Framework.Administration;

public static class ReservationsAdminPermissions
{
    public static readonly AdminPermission Read = AdminPermission.Create(ReservationsAdminPermissionCodes.Read);
    public static readonly AdminPermission Create = AdminPermission.Create(ReservationsAdminPermissionCodes.Create);
    public static readonly AdminPermission Manage = AdminPermission.Create(ReservationsAdminPermissionCodes.Manage);
    public static readonly AdminPermission Cancel = AdminPermission.Create(ReservationsAdminPermissionCodes.Cancel);
}
