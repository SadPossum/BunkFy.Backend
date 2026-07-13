namespace BunkFy.Extensions.Operations.Notifications;

using System.Text.Json;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Notifications.Contracts;

[IntegrationEventHandler("bunkfy-property-retired-notification", RequiresExplicitProducerBinding = true)]
internal sealed class PropertyRetiredNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(PropertyRetiredIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            new OperationalNotification(
                PropertiesModuleMetadata.Name,
                "property-retired",
                "Property retired",
                "A property was retired and is no longer available for normal operations.",
                NotificationSeverity.Warning,
                JsonSerializer.Serialize(new
                {
                    integrationEvent.PropertyId,
                    integrationEvent.PropertyVersion,
                }),
                BunkFyNotificationTags.PropertyActivity),
            cancellationToken);
}
