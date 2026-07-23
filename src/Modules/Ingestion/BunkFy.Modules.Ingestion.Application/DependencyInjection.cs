namespace BunkFy.Modules.Ingestion.Application;

using Gma.Framework.Application.Composition;
using Gma.Framework.AccessControl;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Tasks;
using Gma.Framework.Tasks;
using Gma.Framework.Messaging;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Application.Ports;
using Microsoft.Extensions.DependencyInjection.Extensions;
using global::BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Ingestion.Application.Reservations;
using Microsoft.Extensions.DependencyInjection;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.ProjectionRebuild.Tasks;
using BunkFy.Modules.Ingestion.Application.Credentials;
using BunkFy.Modules.Ingestion.Application.Parsing;

public static class DependencyInjection
{
    public static IServiceCollection AddIngestionApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddGmaAccessControlPermissionPolicies(IngestionModuleMetadata.Descriptor);
        services.TryAddScoped<IIngestionCountryPolicyAdmission, IngestionCountryPolicyAdmission>();
        services.TryAddScoped<IAdapterObservationSinkFactory, AdapterObservationSinkFactory>();
        services.TryAddScoped<IAdapterDescriptorRegistry, AdapterDescriptorRegistry>();
        services.TryAddScoped<IAdapterRunnerRegistry, AdapterRunnerRegistry>();
        services.TryAddScoped<IObservationParserDescriptorRegistry, ObservationParserDescriptorRegistry>();
        services.TryAddScoped<IObservationParserRegistry, ObservationParserRegistry>();
        services.TryAddSingleton<IAdapterIngressTokenService, AdapterIngressTokenService>();
        services.TryAddScoped<IAdapterIngressAuthenticator, AdapterIngressAuthenticator>();
        services.TryAddScoped<ReservationObservationPayloadLoader>();
        services.TryAddScoped<ReservationExternalRequestPublisher>();
        services.AddIntegrationEventHandler<ObservationReceiptAcceptedIntegrationEvent, ObservationReceiptAcceptedHandler>(
            IngestionModuleMetadata.Name,
            IngestionModuleMetadata.Name);
        services.AddIntegrationEventHandler<ExternalReservationOperationCompletedIntegrationEvent, ReservationOperationOutcomeHandler>(
            IngestionModuleMetadata.Name,
            ReservationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<ReservationCancelledIntegrationEvent, ReservationCancelledForIngestionHandler>(
            IngestionModuleMetadata.Name,
            ReservationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyCreatedIntegrationEvent, IngestionPropertyCreatedHandler>(
            IngestionModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyUpdatedIntegrationEvent, IngestionPropertyUpdatedHandler>(
            IngestionModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<PropertyRetiredIntegrationEvent, IngestionPropertyRetiredHandler>(
            IngestionModuleMetadata.Name,
            PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyProcessingPolicyActivatedIntegrationEvent,
            IngestionPropertyProcessingPolicyActivatedHandler>(
                IngestionModuleMetadata.Name,
                PropertiesModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            PropertyProcessingSuspendedIntegrationEvent,
            IngestionPropertyProcessingSuspendedHandler>(
                IngestionModuleMetadata.Name,
                PropertiesModuleMetadata.Name);

        return services;
    }

    public static IServiceCollection AddIngestionTaskHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProjectionRebuildTasks();
        services.AddTaskHandler<RunAdapterTaskPayload, RunAdapterTaskHandler>(IngestionModuleMetadata.Name);
        services.AddTaskHandler<RebuildIngestionPropertiesPayload, RebuildIngestionPropertiesTaskHandler>(
            IngestionModuleMetadata.Name);
        services.AddTaskHandler<PurgeExpiredRawPayloadsPayload, PurgeExpiredRawPayloadsTaskHandler>(
            IngestionModuleMetadata.Name);
        services.AddTaskHandler<RedactExpiredReservationHistoryPayload, RedactExpiredReservationHistoryTaskHandler>(
            IngestionModuleMetadata.Name);
        services.AddTaskHandler<ReprocessObservationPayload, ReprocessObservationTaskHandler>(
            IngestionModuleMetadata.Name);
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<ITaskScheduleProvider, IngestionPollingScheduleProvider>());
        return services;
    }
}
