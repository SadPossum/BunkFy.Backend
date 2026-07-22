namespace BunkFy.Extensions.Operations.Notifications;

using Gma.Modules.Notifications.Contracts;

internal sealed record OperationalNotification(
    string SourceModule,
    string Name,
    string Title,
    string Body,
    NotificationSeverity Severity,
    IOperationalNotificationPayload Payload,
    IReadOnlyList<NotificationTag> Tags,
    string? ActorId = null);
