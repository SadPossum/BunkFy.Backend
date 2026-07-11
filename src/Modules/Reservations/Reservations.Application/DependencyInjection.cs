namespace Reservations.Application;

using Gma.Framework.Application.Composition;
using Gma.Framework.AccessControl;
using Gma.Framework.Messaging;
using Inventory.Contracts;
using Reservations.Application.Handlers;
using Reservations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using Reservations.Application.Tasks;
public static class DependencyInjection
{
    public static IServiceCollection AddReservationsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddGmaAccessControlPermissionPolicies(ReservationsModuleMetadata.Descriptor);
        services.AddIntegrationEventHandler<InventoryAllocationConfirmedIntegrationEvent, InventoryAllocationConfirmedHandler>(
            ReservationsModuleMetadata.Name,
            InventoryModuleMetadata.Name);
        services.AddIntegrationEventHandler<InventoryAllocationRejectedIntegrationEvent, InventoryAllocationRejectedHandler>(
            ReservationsModuleMetadata.Name,
            InventoryModuleMetadata.Name);
        services.AddIntegrationEventHandler<InventoryAllocationReleasedIntegrationEvent, InventoryAllocationReleasedHandler>(
            ReservationsModuleMetadata.Name,
            InventoryModuleMetadata.Name);
        services.AddIntegrationEventHandler<InventoryAllocationReleaseRejectedIntegrationEvent, InventoryAllocationReleaseRejectedHandler>(
            ReservationsModuleMetadata.Name,
            InventoryModuleMetadata.Name);
        services.AddIntegrationEventHandler<InventoryUnitDefinitionChangedIntegrationEvent, InventoryUnitDefinitionChangedHandler>(
            ReservationsModuleMetadata.Name,
            InventoryModuleMetadata.Name);
        services.AddIntegrationEventHandler<ManualInventoryBlockCreatedIntegrationEvent, InventoryManualBlockCreatedHandler>(
            ReservationsModuleMetadata.Name,
            InventoryModuleMetadata.Name);
        services.AddIntegrationEventHandler<ManualInventoryBlockReleasedIntegrationEvent, InventoryManualBlockReleasedHandler>(
            ReservationsModuleMetadata.Name,
            InventoryModuleMetadata.Name);

        return services;
    }

    public static IServiceCollection AddReservationsTaskHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProjectionRebuildTasks();
        services.AddTaskHandler<RebuildReservationInventoryProjectionPayload, RebuildReservationInventoryProjectionTaskHandler>(
            ReservationsModuleMetadata.Name);

        return services;
    }
}
