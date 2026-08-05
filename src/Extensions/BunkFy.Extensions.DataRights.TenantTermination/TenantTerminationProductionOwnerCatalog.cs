namespace BunkFy.Extensions.DataRights.TenantTermination;

using BunkFy.Extensions.DataRights.AccessControl;
using BunkFy.Extensions.DataRights.Organizations;
using BunkFy.Extensions.DataRights.TaskRuntime;
using BunkFy.Extensions.Operations.Notifications;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Contracts;

public static class TenantTerminationProductionOwnerCatalog
{
    public static IReadOnlyList<string> RequiredOwnerKeys { get; } =
    [
        AccessControlTenantTerminationMetadata.OwnerKey,
        GuestsTenantTerminationMetadata.OwnerKey,
        IngestionTenantTerminationMetadata.OwnerKey,
        InventoryTenantTerminationMetadata.OwnerKey,
        OperationsNotificationsTenantTerminationMetadata.OwnerKey,
        OrganizationsTenantTerminationMetadata.OwnerKey,
        PropertiesTenantTerminationMetadata.OwnerKey,
        ReservationsTenantTerminationMetadata.OwnerKey,
        RetentionTenantTerminationMetadata.OwnerKey,
        StaffTenantTerminationMetadata.OwnerKey,
        TaskRuntimeTenantTerminationMetadata.OwnerKey,
        WorkspacesTenantTerminationMetadata.OwnerKey
    ];
}
