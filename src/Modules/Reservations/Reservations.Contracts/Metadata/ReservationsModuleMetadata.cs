namespace Reservations.Contracts;

using Gma.Framework.Modules;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;
using Inventory.Contracts;

public static class ReservationsModuleMetadata
{
    public const string Name = "reservations";
    public const string Schema = "reservations";
    public const string InventoryProjectionName = "inventory-availability";
    public const int InventoryProjectionVersion = 1;
    public const string ProjectionWorkerGroup = "projection-workers";
    public const string AllocationConfirmedHandlerName = "allocation-confirmed";
    public const string AllocationRejectedHandlerName = "allocation-rejected";
    public const string AllocationReleasedHandlerName = "allocation-released";
    public const string AllocationReleaseRejectedHandlerName = "allocation-release-rejected";
    public const string UnitDefinitionChangedHandlerName = "inventory-unit-definition-changed";
    public const string ManualBlockCreatedHandlerName = "manual-inventory-block-created";
    public const string ManualBlockReleasedHandlerName = "manual-inventory-block-released";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithPermissions([
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.Read, "Read reservations.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.Create, "Create reservations.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.Manage, "Manage reservation lifecycle.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(ReservationsAdminPermissionCodes.Cancel, "Cancel reservations.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants)
        ])
        .WithSubscription<InventoryAllocationConfirmedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationConfirmedHandlerName)
        .WithSubscription<InventoryAllocationRejectedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationRejectedHandlerName)
        .WithSubscription<InventoryAllocationReleasedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationReleasedHandlerName)
        .WithSubscription<InventoryAllocationReleaseRejectedIntegrationEvent>(InventoryModuleMetadata.Name, AllocationReleaseRejectedHandlerName)
        .WithSubscription<InventoryUnitDefinitionChangedIntegrationEvent>(InventoryModuleMetadata.Name, UnitDefinitionChangedHandlerName)
        .WithSubscription<ManualInventoryBlockCreatedIntegrationEvent>(InventoryModuleMetadata.Name, ManualBlockCreatedHandlerName)
        .WithSubscription<ManualInventoryBlockReleasedIntegrationEvent>(InventoryModuleMetadata.Name, ManualBlockReleasedHandlerName)
        .WithPublishedEvent<ReservationCreatedIntegrationEvent>()
        .WithPublishedEvent<ReservationConfirmedIntegrationEvent>()
        .WithPublishedEvent<ReservationAllocationRejectedIntegrationEvent>()
        .WithPublishedEvent<ReservationCancelledIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationRequestedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationReleaseRequestedIntegrationEvent>()
        .WithTask<RebuildReservationInventoryProjectionPayload>()
        .WithProfile(ReservationsProfiles.Default)
        .Build();
}
