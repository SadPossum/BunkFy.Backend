namespace BunkFy.Extensions.Operations.Notifications;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Permissions;

internal sealed record OperationalNotification(
    string SourceModule,
    string Name,
    string Title,
    string Body,
    NotificationSeverity Severity,
    IOperationalNotificationPayload Payload,
    IReadOnlyList<NotificationTag> Tags,
    string? ActorId = null)
{
    public IReadOnlyList<NotificationHistoryReference> References { get; init; } =
        [];
    public PermissionCode? RequiredPermission { get; init; }
    public NotificationDeliveryPolicy DeliveryPolicy { get; init; } =
        NotificationDeliveryPolicy.RespectPreferences;
}
