namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;

internal static class OperationsNotificationsStaffDataRightsExportSchema
{
    public const string ExportSchemaId =
        "operations-notifications.staff-inbox-history-export";
    public const int ExportSchemaVersion = 1;

    private static readonly
        OperationsNotificationsDataRightsExportSchemaDefinition<
            StaffNotificationHistoryDataRightsExport> Definition =
        new(
            ExportSchemaId,
            ExportSchemaVersion,
            "include-in-authorized-staff-notification-export",
            "operations-notifications-data-rights-export-fragment");

    public static DataRightsExportDescriptor Descriptor =>
        Definition.Descriptor;

    public static void EnsureValid() => Definition.EnsureValid();

    public static DataRightsExportRecord CreateRecord(
        StaffNotificationHistoryDataRightsExport value)
    {
        ArgumentNullException.ThrowIfNull(value);
        IReadOnlyCollection<(string Member, object? Value)> values =
        [
            (nameof(value.NotificationId), value.NotificationId),
            (nameof(value.SourceModule), value.SourceModule),
            (nameof(value.NotificationName), value.NotificationName),
            (nameof(value.NotificationVersion), value.NotificationVersion),
            (nameof(value.Title), value.Title),
            (nameof(value.Body), value.Body),
            (nameof(value.Severity), value.Severity),
            (nameof(value.StreamSequence), value.StreamSequence),
            (nameof(value.OccurredAtUtc), value.OccurredAtUtc),
            (nameof(value.CreatedAtUtc), value.CreatedAtUtc),
            (nameof(value.Payload), value.Payload),
            (nameof(value.Tags), value.Tags),
            (nameof(value.DeliveryPolicy), value.DeliveryPolicy)
        ];
        return Definition.CreateRecord(
            value.NotificationId,
            value.StreamSequence,
            values);
    }
}
