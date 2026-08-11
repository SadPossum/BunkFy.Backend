namespace BunkFy.Modules.Inventory.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Application.Contributors;
using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Application.Tasks;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<InventoryUnitDefinitionPublisher>();
        services.AddScoped<ManualInventoryBlockCreator>();
        services.AddScoped<BedRetirementCoordinator>();
        services.AddScoped<RoomRetirementCoordinator>();
        services.AddScoped<BedRetirementOutcomeCoordinator>();
        services.AddScoped<RoomRetirementOutcomeCoordinator>();
        services.AddScoped<InventoryRetirementCoordinator>();
        services.AddScoped<InventoryAllocationMutationCoordinator>();
        services.AddScoped<InventoryManagementMutationCoordinator>();
        services.AddScoped<InventoryManagementOperationJournal>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationContributor,
                InventoryDataRightsAnonymisationContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationRestoreContributor,
                InventoryDataRightsAnonymisationRestoreContributor>());
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
        services.AddIntegrationEventHandler<BedRetirementFinalizedIntegrationEvent, BedRetirementFinalizedHandler>(
            InventoryModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<BedRetirementFinalizationRejectedIntegrationEvent, BedRetirementFinalizationRejectedHandler>(
            InventoryModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<RoomRetirementFinalizedIntegrationEvent, RoomRetirementFinalizedHandler>(
            InventoryModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<RoomRetirementFinalizationRejectedIntegrationEvent, RoomRetirementFinalizationRejectedHandler>(
            InventoryModuleMetadata.Name,
            PropertiesModuleMetadata.Name);

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
