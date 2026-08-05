namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.Permissions;
using Gma.Modules.Notifications.Contracts;

[IntegrationEventHandler(
    "bunkfy-data-rights-response-deadline-notification",
    RequiresExplicitProducerBinding = true)]
internal sealed class DataRightsResponseDeadlineNotificationHandler(
    OperationalNotificationProjector projector)
    : IIntegrationEventHandler<
        DataRightsResponseDeadlineAlertDueIntegrationEvent>
{
    internal const string DueSoonNotificationName =
        "data-rights-response-deadline-due-soon";
    internal const string OverdueNotificationName =
        "data-rights-response-deadline-overdue";

    public Task HandleAsync(
        DataRightsResponseDeadlineAlertDueIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            CreateNotification(integrationEvent),
            cancellationToken);

    private static OperationalNotification CreateNotification(
        DataRightsResponseDeadlineAlertDueIntegrationEvent integrationEvent)
    {
        (string name, string title, string body, NotificationSeverity severity) =
            integrationEvent.AlertKind switch
            {
                DataRightsResponseDeadlineAlertKind.DueSoon => (
                    DueSoonNotificationName,
                    "Privacy request due soon",
                    "A guest privacy request is approaching its response deadline.",
                    NotificationSeverity.Warning),
                DataRightsResponseDeadlineAlertKind.Overdue => (
                    OverdueNotificationName,
                    "Privacy request overdue",
                    "A guest privacy request has passed its response deadline.",
                    NotificationSeverity.Error),
                _ => throw new InvalidOperationException(
                    "The Data Rights deadline alert kind is invalid.")
            };

        return new OperationalNotification(
            DataRightsModuleMetadata.Name,
            name,
            title,
            body,
            severity,
            new DataRightsDeadlineNotificationPayload(
                integrationEvent.PropertyId,
                integrationEvent.CaseId),
            BunkFyNotificationTags.DataRightsAttention)
        {
            RequiredPermission = PermissionCode.Create(
                DataRightsAdminPermissionCodes.Read),
            DeliveryPolicy = NotificationDeliveryPolicy.Mandatory
        };
    }
}
