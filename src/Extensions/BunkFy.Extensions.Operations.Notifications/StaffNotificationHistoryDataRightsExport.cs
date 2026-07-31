namespace BunkFy.Extensions.Operations.Notifications;

using System.Text.Json;
using Gma.Modules.Notifications.Contracts;

internal sealed record StaffNotificationHistoryDataRightsExport(
    Guid NotificationId,
    string SourceModule,
    string NotificationName,
    int NotificationVersion,
    string Title,
    string? Body,
    NotificationSeverity Severity,
    long StreamSequence,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    JsonElement Payload,
    IReadOnlyList<string> Tags,
    NotificationDeliveryPolicy DeliveryPolicy);
