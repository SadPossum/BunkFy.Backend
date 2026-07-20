namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddBunkFyOperationsNotifications(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IWorkspaceOwnerNotificationAudienceReader, EmptyWorkspaceOwnerNotificationAudienceReader>();
        services.TryAddScoped<OperationalNotificationProjector>();

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
        Add<ExternalReservationOperationCompletedIntegrationEvent,
            ExternalReservationOperationAttentionNotificationHandler>(
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
}
