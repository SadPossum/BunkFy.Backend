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

        services.TryAddScoped<OperationalNotificationProjector>();

        Add<PropertyRetiredIntegrationEvent, PropertyRetiredNotificationHandler>(
            services,
            PropertiesModuleMetadata.Name,
            PropertyRetiredIntegrationEvent.EventType,
            PropertyRetiredIntegrationEvent.EventVersion,
            "bunkfy-property-retired-notification");

        Add<ManualInventoryBlockCreatedIntegrationEvent, ManualInventoryBlockCreatedNotificationHandler>(
            services,
            InventoryModuleMetadata.Name,
            ManualInventoryBlockCreatedIntegrationEvent.EventType,
            ManualInventoryBlockCreatedIntegrationEvent.EventVersion,
            "bunkfy-inventory-block-created-notification");
        Add<ManualInventoryBlockReleasedIntegrationEvent, ManualInventoryBlockReleasedNotificationHandler>(
            services,
            InventoryModuleMetadata.Name,
            ManualInventoryBlockReleasedIntegrationEvent.EventType,
            ManualInventoryBlockReleasedIntegrationEvent.EventVersion,
            "bunkfy-inventory-block-released-notification");
        Add<RoomSalesModeChangedIntegrationEvent, RoomSalesModeChangedNotificationHandler>(
            services,
            InventoryModuleMetadata.Name,
            RoomSalesModeChangedIntegrationEvent.EventType,
            RoomSalesModeChangedIntegrationEvent.EventVersion,
            "bunkfy-room-sales-mode-changed-notification");

        Add<ReservationConfirmedIntegrationEvent, ReservationConfirmedNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name,
            ReservationConfirmedIntegrationEvent.EventType,
            ReservationConfirmedIntegrationEvent.EventVersion,
            "bunkfy-reservation-confirmed-notification");
        Add<ReservationAllocationRejectedIntegrationEvent, ReservationAllocationRejectedNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name,
            ReservationAllocationRejectedIntegrationEvent.EventType,
            ReservationAllocationRejectedIntegrationEvent.EventVersion,
            "bunkfy-reservation-allocation-rejected-notification");
        Add<ReservationCancelledIntegrationEvent, ReservationCancelledNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name,
            ReservationCancelledIntegrationEvent.EventType,
            ReservationCancelledIntegrationEvent.EventVersion,
            "bunkfy-reservation-cancelled-notification");
        Add<ReservationNoShowIntegrationEvent, ReservationNoShowNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name,
            ReservationNoShowIntegrationEvent.EventType,
            ReservationNoShowIntegrationEvent.EventVersion,
            "bunkfy-reservation-no-show-notification");
        Add<ExternalReservationOperationCompletedIntegrationEvent,
            ExternalReservationOperationAttentionNotificationHandler>(
            services,
            ReservationsModuleMetadata.Name,
            ExternalReservationOperationCompletedIntegrationEvent.EventType,
            ExternalReservationOperationCompletedIntegrationEvent.EventVersion,
            "bunkfy-provider-reservation-conflict-notification");

        Add<StaffPropertyAssignmentChangedIntegrationEvent, StaffPropertyAssignmentChangedNotificationHandler>(
            services,
            StaffModuleMetadata.Name,
            StaffPropertyAssignmentChangedIntegrationEvent.EventType,
            StaffPropertyAssignmentChangedIntegrationEvent.EventVersion,
            "bunkfy-staff-property-assignment-notification");
        Add<StaffMemberLifecycleChangedIntegrationEvent, StaffMemberLifecycleChangedNotificationHandler>(
            services,
            StaffModuleMetadata.Name,
            StaffMemberLifecycleChangedIntegrationEvent.EventType,
            StaffMemberLifecycleChangedIntegrationEvent.EventVersion,
            "bunkfy-staff-lifecycle-notification");

        return services;
    }

    private static void Add<TEvent, THandler>(
        IServiceCollection services,
        string producerModule,
        string eventType,
        int eventVersion,
        string handlerName)
        where TEvent : class, IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent> =>
        services.AddIntegrationEventHandler<TEvent, THandler>(
            NotificationsModuleMetadata.Name,
            producerModule,
            eventType,
            eventVersion,
            handlerName);
}
