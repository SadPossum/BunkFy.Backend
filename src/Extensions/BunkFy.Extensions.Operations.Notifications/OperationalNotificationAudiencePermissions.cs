namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Permissions;

internal static class OperationalNotificationAudiencePermissions
{
    public static PermissionCode PropertiesRead { get; } =
        PermissionCode.Create(PropertiesAdminPermissionCodes.Read);

    public static PermissionCode InventoryRead { get; } =
        PermissionCode.Create(InventoryAdminPermissionCodes.Read);

    public static PermissionCode ReservationsRead { get; } =
        PermissionCode.Create(ReservationsAdminPermissionCodes.Read);

    public static PermissionCode IngestionRead { get; } =
        PermissionCode.Create(IngestionAdminPermissionCodes.Read);

    public static PermissionCode DataRightsRead { get; } =
        PermissionCode.Create(DataRightsAdminPermissionCodes.Read);
}
