namespace BunkFy.Extensions.Operations.Notifications;

using System.Security.Cryptography;
using System.Text;

public static class OperationsNotificationsTenantTerminationMetadata
{
    public const string OwnerKey =
        OperationsNotificationsDataRightsCoordinates.Owner;
    public const string DependencyOwnerKey = "ingestion";
    public const int CatalogVersion = 3;
    public const int PersonalDataCatalogVersion = 8;
    public const string ExportCatalogId =
        "operations-notifications.tenant-portability";
    public const int ExportCatalogSchemaVersion = 1;
    public const string ExportSchemaId =
        "operations-notifications.tenant-termination-export";
    public const int ExportSchemaVersion = 2;

    public const string NotificationRecordType = "operational-notification";
    public const string PreferenceRecordType = "notification-preference";
    public const string DeliveryRouteRecordType = "notification-delivery-route";
    public const string TagDefinitionRecordType = "notification-tag-definition";
    public const string DeliveryRecordType = "notification-delivery";
    public const string DeliveryAttemptRecordType =
        "notification-delivery-attempt";
    public const string BroadcastRecordType = "tenant-broadcast";
    public const string BroadcastReadRecordType = "tenant-broadcast-read";
    public const string HistoryReferenceStateRecordType =
        "notification-history-reference-state";
    public const string HistoryCloseReceiptRecordType =
        "notification-history-close-receipt";
    public const string HistoryBatchCloseOperationRecordType =
        "notification-history-batch-close-operation";
    public const string HistoryBatchCloseReceiptRecordType =
        "notification-history-batch-close-receipt";

    public static IReadOnlyList<string> RecordTypes { get; } =
        Array.AsReadOnly(
        [
            NotificationRecordType,
            PreferenceRecordType,
            DeliveryRouteRecordType,
            TagDefinitionRecordType,
            DeliveryRecordType,
            DeliveryAttemptRecordType,
            BroadcastRecordType,
            BroadcastReadRecordType,
            HistoryReferenceStateRecordType,
            HistoryCloseReceiptRecordType,
            HistoryBatchCloseOperationRecordType,
            HistoryBatchCloseReceiptRecordType
        ]);

    public static IReadOnlyList<string> ExportFieldIds { get; } =
        Array.AsReadOnly(
        [
            "operations-notifications.tenant-export-notification-id",
            "operations-notifications.tenant-export-recipient-id",
            "operations-notifications.tenant-export-source-module",
            "operations-notifications.tenant-export-notification-name",
            "operations-notifications.tenant-export-notification-version",
            "operations-notifications.tenant-export-title",
            "operations-notifications.tenant-export-body",
            "operations-notifications.tenant-export-severity",
            "operations-notifications.tenant-export-stream-sequence",
            "operations-notifications.tenant-export-occurred-at",
            "operations-notifications.tenant-export-created-at",
            "operations-notifications.tenant-export-read-at",
            "operations-notifications.tenant-export-payload",
            "operations-notifications.tenant-export-delivery-policy",
            "operations-notifications.tenant-export-inbox-visible",
            "operations-notifications.tenant-export-tags",
            "operations-notifications.tenant-export-history-references",
            "operations-notifications.tenant-export-preference-id",
            "operations-notifications.tenant-export-user-id",
            "operations-notifications.tenant-export-preference-state",
            "operations-notifications.tenant-export-route-id",
            "operations-notifications.tenant-export-route-state",
            "operations-notifications.tenant-export-updated-by",
            "operations-notifications.tenant-export-tag-definition-id",
            "operations-notifications.tenant-export-tag-definition-state",
            "operations-notifications.tenant-export-created-by",
            "operations-notifications.tenant-export-delivery-id",
            "operations-notifications.tenant-export-delivery-state",
            "operations-notifications.tenant-export-delivery-attempt-id",
            "operations-notifications.tenant-export-delivery-attempt-state",
            "operations-notifications.tenant-export-broadcast-id",
            "operations-notifications.tenant-export-broadcast-audience",
            "operations-notifications.tenant-export-broadcast-message",
            "operations-notifications.tenant-export-broadcast-read-id",
            "operations-notifications.tenant-export-recipient-kind",
            "operations-notifications.tenant-export-history-reference",
            "operations-notifications.tenant-export-history-lifecycle-proof"
        ]);

    public static string CatalogManifest { get; } = string.Join(
        '|',
        OwnerKey,
        "contract=1",
        "phases=export,destroy",
        $"dependencies={DependencyOwnerKey}",
        "mandatory=true",
        $"catalog={CatalogVersion}",
        $"personal-data-catalog={PersonalDataCatalogVersion}",
        $"export-schema={ExportSchemaId}:{ExportSchemaVersion}",
        "scope-lifecycle=notifications:v1",
        "records=" + string.Join(',', RecordTypes),
        "fields=" + string.Join(',', ExportFieldIds));

    public static string CatalogSha256 { get; } =
        Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(CatalogManifest)));
}
