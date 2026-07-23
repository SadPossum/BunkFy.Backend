namespace BunkFy.Modules.Guests.Application;

using BunkFy.DataGovernance;
using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using BunkFy.Modules.Guests.Application.Handlers;
using BunkFy.Modules.Guests.Application.Policies;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Application.Tasks;
using BunkFy.Modules.Guests.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BunkFy.Modules.Properties.Contracts;

public static class DependencyInjection
{
    public static IServiceCollection AddGuestsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddGmaAccessControlPermissionPolicies(GuestsModuleMetadata.Descriptor);
        services.TryAddSingleton(_ => CountryPolicyRegistry.Create(
            [],
            [],
            CountryPolicyRuntimeMode.Engineering));
        services.TryAddScoped<IGuestCountryPolicyAdmission, GuestCountryPolicyAdmission>();
        services.AddIntegrationEventHandler<PropertyCreatedIntegrationEvent, GuestPropertyCreatedHandler>(
            GuestsModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyUpdatedIntegrationEvent, GuestPropertyUpdatedHandler>(
            GuestsModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyRetiredIntegrationEvent, GuestPropertyRetiredHandler>(
            GuestsModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyProcessingPolicyActivatedIntegrationEvent,
            GuestPropertyProcessingPolicyActivatedHandler>(
                GuestsModuleMetadata.Name,
                PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyProcessingSuspendedIntegrationEvent,
            GuestPropertyProcessingSuspendedHandler>(
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
