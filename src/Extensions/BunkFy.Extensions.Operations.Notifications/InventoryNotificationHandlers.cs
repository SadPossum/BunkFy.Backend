namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Notifications.Contracts;

[IntegrationEventHandler("bunkfy-inventory-block-created-notification", RequiresExplicitProducerBinding = true)]
internal sealed class ManualInventoryBlockCreatedNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<ManualInventoryBlockCreatedIntegrationEvent>
{
    public Task HandleAsync(
        ManualInventoryBlockCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.BlockGroupId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            new OperationalNotification(
                InventoryModuleMetadata.Name,
                "manual-inventory-block-created",
                "Inventory blocked",
                $"Inventory was blocked from {integrationEvent.Arrival:yyyy-MM-dd} through {integrationEvent.Departure:yyyy-MM-dd}.",
                NotificationSeverity.Info,
                new InventoryBlockCreatedNotificationPayload(
                    integrationEvent.PropertyId,
                    integrationEvent.BlockGroupId,
                    integrationEvent.Arrival,
                    integrationEvent.Departure),
                BunkFyNotificationTags.InventoryActivity,
                integrationEvent.ActorId),
            cancellationToken);
}

[IntegrationEventHandler("bunkfy-inventory-block-released-notification", RequiresExplicitProducerBinding = true)]
internal sealed class ManualInventoryBlockReleasedNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<ManualInventoryBlockReleasedIntegrationEvent>
{
    public Task HandleAsync(
        ManualInventoryBlockReleasedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.BlockGroupId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            new OperationalNotification(
                InventoryModuleMetadata.Name,
                "manual-inventory-block-released",
                "Inventory block released",
                "A manual inventory block was released.",
                NotificationSeverity.Info,
                new InventoryBlockReleasedNotificationPayload(
                    integrationEvent.PropertyId,
                    integrationEvent.BlockGroupId),
                BunkFyNotificationTags.InventoryActivity,
                integrationEvent.ActorId),
            cancellationToken);
}

[IntegrationEventHandler("bunkfy-room-sales-mode-changed-notification", RequiresExplicitProducerBinding = true)]
internal sealed class RoomSalesModeChangedNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<RoomSalesModeChangedIntegrationEvent>
{
    public Task HandleAsync(
        RoomSalesModeChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            new OperationalNotification(
                InventoryModuleMetadata.Name,
                "room-sales-mode-changed",
                "Room sales mode changed",
                $"A room now sells at {integrationEvent.SalesMode} level.",
                NotificationSeverity.Warning,
                new RoomNotificationPayload(
                    integrationEvent.PropertyId,
                    integrationEvent.RoomId),
                BunkFyNotificationTags.InventoryActivity,
                integrationEvent.ActorId),
            cancellationToken);
}
