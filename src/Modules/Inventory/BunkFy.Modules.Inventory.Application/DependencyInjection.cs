namespace BunkFy.Modules.Inventory.Application;

using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Application.Tasks;
using BunkFy.Modules.Inventory.Contracts;
using Microsoft.Extensions.DependencyInjection;
using BunkFy.Modules.Properties.Contracts;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<InventoryUnitDefinitionPublisher>();
        services.AddGmaAccessControlPermissionPolicies(InventoryModuleMetadata.Descriptor);
        services.AddIntegrationEventHandler<PropertyCreatedIntegrationEvent, PropertyCreatedTopologyHandler>(InventoryModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyUpdatedIntegrationEvent, PropertyUpdatedTopologyHandler>(InventoryModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyRetiredIntegrationEvent, PropertyRetiredTopologyHandler>(InventoryModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<RoomCreatedIntegrationEvent, RoomCreatedTopologyHandler>(InventoryModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<RoomUpdatedIntegrationEvent, RoomUpdatedTopologyHandler>(InventoryModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<RoomRetiredIntegrationEvent, RoomRetiredTopologyHandler>(InventoryModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<BedAddedIntegrationEvent, BedAddedTopologyHandler>(InventoryModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<BedUpdatedIntegrationEvent, BedUpdatedTopologyHandler>(InventoryModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<BedRetiredIntegrationEvent, BedRetiredTopologyHandler>(InventoryModuleMetadata.Name, PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<InventoryAllocationRequestedIntegrationEvent, InventoryAllocationRequestedHandler>(
            InventoryModuleMetadata.Name,
            InventoryModuleMetadata.ReservationsProducerModuleName);
        services.AddIntegrationEventHandler<InventoryAllocationAmendmentRequestedIntegrationEvent, InventoryAllocationAmendmentRequestedHandler>(
            InventoryModuleMetadata.Name,
            InventoryModuleMetadata.ReservationsProducerModuleName);
        services.AddIntegrationEventHandler<InventoryAllocationReleaseRequestedIntegrationEvent, InventoryAllocationReleaseRequestedHandler>(
            InventoryModuleMetadata.Name,
            InventoryModuleMetadata.ReservationsProducerModuleName);

        return services;
    }

    public static IServiceCollection AddInventoryTaskHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProjectionRebuildTasks();
        services.AddTaskHandler<RebuildInventoryTopologyPayload, RebuildInventoryTopologyTaskHandler>(
            InventoryModuleMetadata.Name);

        return services;
    }
}
