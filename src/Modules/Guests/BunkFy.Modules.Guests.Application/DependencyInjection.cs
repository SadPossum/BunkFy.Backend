namespace BunkFy.Modules.Guests.Application;

using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using BunkFy.Modules.Guests.Application.Handlers;
using BunkFy.Modules.Guests.Application.Tasks;
using BunkFy.Modules.Guests.Contracts;
using Microsoft.Extensions.DependencyInjection;
using BunkFy.Modules.Properties.Contracts;

public static class DependencyInjection
{
    public static IServiceCollection AddGuestsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddGmaAccessControlPermissionPolicies(GuestsModuleMetadata.Descriptor);
        services.AddIntegrationEventHandler<PropertyCreatedIntegrationEvent, GuestPropertyCreatedHandler>(
            GuestsModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyUpdatedIntegrationEvent, GuestPropertyUpdatedHandler>(
            GuestsModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyRetiredIntegrationEvent, GuestPropertyRetiredHandler>(
            GuestsModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<ReservationGuestLinkedIntegrationEvent, ReservationGuestLinkedStayHandler>(
            GuestsModuleMetadata.Name,
            GuestsModuleMetadata.ReservationsProducerModuleName);
        services.AddIntegrationEventHandler<ReservationGuestStayChangedIntegrationEvent, ReservationGuestStayChangedHandler>(
            GuestsModuleMetadata.Name,
            GuestsModuleMetadata.ReservationsProducerModuleName);
        return services;
    }

    public static IServiceCollection AddGuestsTaskHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProjectionRebuildTasks();
        services.AddTaskHandler<RebuildGuestsPropertiesPayload, RebuildGuestsPropertiesTaskHandler>(
            GuestsModuleMetadata.Name);
        services.AddTaskHandler<RebuildGuestStayHistoryPayload, RebuildGuestStayHistoryTaskHandler>(
            GuestsModuleMetadata.Name);
        return services;
    }
}
