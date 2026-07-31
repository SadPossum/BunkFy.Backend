namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IHostApplicationBuilder
        AddBunkFyOperationsNotificationsProductionAdmission(
            this IHostApplicationBuilder builder,
            OperationsNotificationsProductionHostRole hostRole,
            bool notificationsComposed = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!Enum.IsDefined(hostRole))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hostRole),
                hostRole,
                "The Operations Notifications production host role is invalid.");
        }

        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType ==
                typeof(OperationsNotificationsProductionAdmissionMarker)))
        {
            throw new InvalidOperationException(
                "Operations Notifications production admission is already registered.");
        }

        OperationsNotificationsProductionAdmissionRegistration registration =
            new(
                builder.Environment.IsProduction(),
                hostRole,
                notificationsComposed,
                string.Equals(
                    builder.Configuration[
                        "BunkFy:Deployment:ApiTopology"],
                    "SingleReplica",
                    StringComparison.OrdinalIgnoreCase));
        OperationsNotificationsRetentionRuntimeOptions runtime =
            ReadRetentionRuntime(builder.Configuration);
        OperationsNotificationsPersonalDataCatalogEvidence catalog =
            OperationsNotificationsPersonalDataCatalog.Current;
        IConfigurationSection section = builder.Configuration.GetSection(
            OperationsNotificationsProductionAdmissionOptions.SectionName);
        OperationsNotificationsProductionAdmissionOptions options =
            section.Get<
                OperationsNotificationsProductionAdmissionOptions>() ??
            new OperationsNotificationsProductionAdmissionOptions();
        OperationsNotificationsProductionAdmissionValidator validator =
            new(registration, catalog, runtime);
        ValidateOptionsResult validation =
            validator.Validate(name: null, options);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                OperationsNotificationsProductionAdmissionOptions.SectionName,
                typeof(OperationsNotificationsProductionAdmissionOptions),
                validation.Failures);
        }

        builder.Services.AddSingleton<
            OperationsNotificationsProductionAdmissionMarker>();
        builder.Services.AddSingleton(registration);
        builder.Services.AddSingleton(runtime);
        builder.Services.AddSingleton(catalog);
        builder.Services
            .AddOptions<
                OperationsNotificationsProductionAdmissionOptions>()
            .Bind(section)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<
                    OperationsNotificationsProductionAdmissionOptions>>(
                validator));
        if (registration.IsProduction)
        {
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IHostedService,
                    OperationsNotificationsProductionAdmissionReporter>());
        }

        return builder;
    }

    public static IServiceCollection AddBunkFyOperationsNotifications(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IWorkspaceOwnerNotificationAudienceReader, EmptyWorkspaceOwnerNotificationAudienceReader>();
        services.TryAddScoped<OperationalNotificationProjector>();
        OperationsNotificationsDataRightsExportSchema.EnsureValid();
        OperationsNotificationsStaffDataRightsExportSchema.EnsureValid();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsRequiredCompanionContributor,
                OperationsNotificationsReservationAccessExportCompanionContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsRequiredCompanionContributor,
                OperationsNotificationsReservationAnonymisationCompanionContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsRequiredCompanionContributor,
                OperationsNotificationsIngestionAccessExportCompanionContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsRequiredCompanionContributor,
                OperationsNotificationsIngestionAnonymisationCompanionContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsRequiredCompanionContributor,
                OperationsNotificationsStaffAccessExportCompanionContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsRequiredCompanionContributor,
                OperationsNotificationsStaffAnonymisationCompanionContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsSubjectDiscoveryContributor,
                OperationsNotificationsDataRightsDiscoveryContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsSubjectExportContributor,
                OperationsNotificationsDataRightsExportContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsSubjectExportContributor,
                OperationsNotificationsStaffDataRightsExportContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationContributor,
                OperationsNotificationsDataRightsAnonymisationContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationRestoreContributor,
                OperationsNotificationsDataRightsAnonymisationRestoreContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationRestoreContributor,
                OperationsNotificationsIngestionDataRightsAnonymisationRestoreContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationPolicyContributor,
                OperationsNotificationsStaffAnonymisationPolicyContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationExecutionPrerequisiteV2,
                OperationsNotificationsStaffAnonymisationPrerequisite>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationRestorePrerequisiteV3,
                OperationsNotificationsStaffAnonymisationPrerequisite>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationContributorV2,
                OperationsNotificationsStaffAnonymisationContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsAnonymisationRestoreContributorV3,
                OperationsNotificationsStaffAnonymisationRestoreContributor>());

        Add<PropertyRetiredIntegrationEvent, PropertyRetiredNotificationHandler>(
            services,
            PropertiesModuleMetadata.Name);

        Add<ManualInventoryBlockCreatedIntegrationEvent, ManualInventoryBlockCreatedNotificationHandler>(
            services,
            InventoryModuleMetadata.Name);
        Add<ManualInventoryBlockReleasedIntegrationEvent, ManualInventoryBlockReleasedNotificationHandler>(
            services,
            InventoryModuleMetadata.Name);
        Add<RoomSalesModeChangedIntegrationEvent, RoomSalesModeChangedNotificationHandler>(
            services,
            InventoryModuleMetadata.Name);

        Add<ReservationConfirmedIntegrationEvent, ReservationConfirmedNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name);
        Add<ReservationArrivalReminderDueIntegrationEvent, ReservationArrivalReminderNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name);
        Add<ReservationArrivalReminderDueIntegrationEventV2, ReservationArrivalReminderV2NotificationHandler>(
            services,
            ReservationsModuleMetadata.Name);
        Add<ReservationAllocationRejectedIntegrationEvent, ReservationAllocationRejectedNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name);
        Add<ReservationCancelledIntegrationEvent, ReservationCancelledNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name);
        Add<ReservationNoShowIntegrationEvent, ReservationNoShowNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name);
        Add<StaffPropertyAssignmentChangedIntegrationEvent, StaffPropertyAssignmentChangedNotificationHandler>(
            services,
            StaffModuleMetadata.Name);
        Add<StaffMemberLifecycleChangedIntegrationEvent, StaffMemberLifecycleChangedNotificationHandler>(
            services,
            StaffModuleMetadata.Name);

        return services;
    }

    public static IServiceCollection
        AddBunkFyOperationsIngestionNotifications(
            this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Add<ExternalReservationOperationCompletedIntegrationEvent,
            ExternalReservationOperationAttentionNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name);
        return services;
    }

    public static IServiceCollection AddBunkFyWorkspaceOwnerNotificationAudience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Scoped<
            IWorkspaceOwnerNotificationAudienceReader,
            WorkspaceOwnerNotificationAudienceReader>());
        return services;
    }

    private static void Add<TEvent, THandler>(
        IServiceCollection services,
        string producerModule)
        where TEvent : class, IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent> =>
        services.AddIntegrationEventHandler<TEvent, THandler>(
            NotificationsModuleMetadata.Name,
            producerModule);

    private static OperationsNotificationsRetentionRuntimeOptions
        ReadRetentionRuntime(IConfiguration configuration) =>
        new(
            configuration.GetValue(
                "Notifications:Retention:Enabled",
                defaultValue: false),
            configuration.GetValue(
                "Notifications:Retention:ReadHistoryDays",
                defaultValue: 90),
            configuration.GetValue(
                "Notifications:Retention:UnreadHistoryDays",
                defaultValue: 365),
            configuration.GetValue(
                "Notifications:Retention:BroadcastDays",
                defaultValue: 365),
            configuration.GetValue(
                "Notifications:Delivery:AttemptRetentionDays",
                defaultValue: 90));

    private sealed class
        OperationsNotificationsProductionAdmissionMarker;
}
