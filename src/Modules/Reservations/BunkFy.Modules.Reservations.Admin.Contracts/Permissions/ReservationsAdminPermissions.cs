namespace BunkFy.Modules.Reservations.Admin.Contracts;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Administration;

public static class ReservationsAdminPermissions
{
    public static readonly AdminPermission Read = AdminPermission.Create(ReservationsAdminPermissionCodes.Read);
    public static readonly AdminPermission Create = AdminPermission.Create(ReservationsAdminPermissionCodes.Create);
    public static readonly AdminPermission Manage = AdminPermission.Create(ReservationsAdminPermissionCodes.Manage);
    public static readonly AdminPermission Cancel = AdminPermission.Create(ReservationsAdminPermissionCodes.Cancel);
    public static readonly AdminPermission CheckIn = AdminPermission.Create(ReservationsAdminPermissionCodes.CheckIn);
    public static readonly AdminPermission NoShow = AdminPermission.Create(ReservationsAdminPermissionCodes.NoShow);
    public static readonly AdminPermission CheckOut = AdminPermission.Create(ReservationsAdminPermissionCodes.CheckOut);
    public static readonly AdminPermission ManageGuests = AdminPermission.Create(ReservationsAdminPermissionCodes.ManageGuests);
}
