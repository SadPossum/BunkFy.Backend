namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;
using BunkFy.Modules.Properties.Contracts;

public static class InventoryModuleMetadata
{
    public const string Name = "inventory";
    public const string Schema = "inventory";
    public const string TopologyProjectionName = "properties-topology";
    public const int TopologyProjectionVersion = 1;
    public const string ProjectionWorkerGroup = "projection-workers";
    public const string PropertyCreatedHandlerName = "property-created-topology";
    public const string PropertyUpdatedHandlerName = "property-updated-topology";
    public const string PropertyRetiredHandlerName = "property-retired-topology";
    public const string RoomCreatedHandlerName = "room-created-topology";
    public const string RoomUpdatedHandlerName = "room-updated-topology";
    public const string RoomRetiredHandlerName = "room-retired-topology";
    public const string BedAddedHandlerName = "bed-added-topology";
    public const string BedUpdatedHandlerName = "bed-updated-topology";
    public const string BedRetiredHandlerName = "bed-retired-topology";
    public const string AllocationRequestedHandlerName = "allocation-requested";
    public const string AllocationAmendmentRequestedHandlerName = "allocation-amendment-requested";
    public const string AllocationReleaseRequestedHandlerName = "allocation-release-requested";
    public const string ReservationsProducerModuleName = "reservations";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithPermissions([
            new ModulePermissionDescriptor(InventoryAdminPermissionCodes.Read, "Read inventory configuration.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(InventoryAdminPermissionCodes.Configure, "Configure inventory sales modes.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(InventoryAdminPermissionCodes.BlocksManage, "Manage inventory blocks.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
        ])
        .WithSubscription<PropertyCreatedIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyCreatedHandlerName)
        .WithSubscription<PropertyUpdatedIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyUpdatedHandlerName)
        .WithSubscription<PropertyRetiredIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyRetiredHandlerName)
        .WithSubscription<RoomCreatedIntegrationEvent>(PropertiesModuleMetadata.Name, RoomCreatedHandlerName)
        .WithSubscription<RoomUpdatedIntegrationEvent>(PropertiesModuleMetadata.Name, RoomUpdatedHandlerName)
        .WithSubscription<RoomRetiredIntegrationEvent>(PropertiesModuleMetadata.Name, RoomRetiredHandlerName)
        .WithSubscription<BedAddedIntegrationEvent>(PropertiesModuleMetadata.Name, BedAddedHandlerName)
        .WithSubscription<BedUpdatedIntegrationEvent>(PropertiesModuleMetadata.Name, BedUpdatedHandlerName)
        .WithSubscription<BedRetiredIntegrationEvent>(PropertiesModuleMetadata.Name, BedRetiredHandlerName)
        .WithSubscription<InventoryAllocationRequestedIntegrationEvent>(ReservationsProducerModuleName, AllocationRequestedHandlerName)
        .WithSubscription<InventoryAllocationAmendmentRequestedIntegrationEvent>(ReservationsProducerModuleName, AllocationAmendmentRequestedHandlerName)
        .WithSubscription<InventoryAllocationReleaseRequestedIntegrationEvent>(ReservationsProducerModuleName, AllocationReleaseRequestedHandlerName)
        .WithPublishedEvent<RoomSalesModeChangedIntegrationEvent>()
        .WithPublishedEvent<InventoryUnitDefinitionChangedIntegrationEvent>()
        .WithPublishedEvent<ManualInventoryBlockCreatedIntegrationEvent>()
        .WithPublishedEvent<ManualInventoryBlockReleasedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationConfirmedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationRejectedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationAmendmentConfirmedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationAmendmentRejectedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationReleasedIntegrationEvent>()
        .WithPublishedEvent<InventoryAllocationReleaseRejectedIntegrationEvent>()
        .WithTask<RebuildInventoryTopologyPayload>()
        .WithProfile(InventoryProfiles.Default)
        .Build();
}
