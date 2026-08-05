namespace BunkFy.Extensions.Operations.Notifications;

using System.Text.Json;
using Gma.Modules.Notifications.Contracts;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class OperationsNotificationsTenantExportFieldAttribute(
    string fieldId) : Attribute
{
    public string FieldId { get; } = fieldId;
}

internal sealed record OperationsNotificationTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-notification-id")]
    Guid NotificationId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-recipient-id")]
    string RecipientId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-source-module")]
    string SourceModule,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-notification-name")]
    string NotificationName,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-notification-version")]
    int NotificationVersion,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-title")]
    string Title,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-body")]
    string? Body,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-severity")]
    NotificationSeverity Severity,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-stream-sequence")]
    long StreamSequence,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-occurred-at")]
    DateTimeOffset OccurredAtUtc,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-created-at")]
    DateTimeOffset CreatedAtUtc,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-read-at")]
    DateTimeOffset? ReadAtUtc,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-payload")]
    JsonElement Payload,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-delivery-policy")]
    NotificationDeliveryPolicy DeliveryPolicy,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-inbox-visible")]
    bool IsInboxVisible,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-tags")]
    IReadOnlyList<string> Tags,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-history-references")]
    IReadOnlyList<OperationsNotificationHistoryReferenceTenantExport>
        References);

internal sealed record OperationsNotificationPreferenceTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-preference-id")]
    Guid PreferenceId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-user-id")]
    string UserId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-preference-state")]
    OperationsNotificationPreferenceStateTenantExport State);

internal sealed record OperationsNotificationDeliveryRouteTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-route-id")]
    Guid RouteId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-route-state")]
    OperationsNotificationDeliveryRouteStateTenantExport State,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-updated-by")]
    string UpdatedBy);

internal sealed record OperationsNotificationTagDefinitionTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-tag-definition-id")]
    Guid DefinitionId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-tag-definition-state")]
    OperationsNotificationTagDefinitionStateTenantExport State,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-created-by")]
    string CreatedBy,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-updated-by")]
    string UpdatedBy);

internal sealed record OperationsNotificationDeliveryTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-delivery-id")]
    Guid DeliveryId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-notification-id")]
    Guid NotificationId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-delivery-state")]
    OperationsNotificationDeliveryStateTenantExport State);

internal sealed record OperationsNotificationDeliveryAttemptTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-delivery-attempt-id")]
    Guid AttemptId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-delivery-id")]
    Guid DeliveryId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-delivery-attempt-state")]
    OperationsNotificationDeliveryAttemptStateTenantExport State);

internal sealed record OperationsNotificationBroadcastTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-broadcast-id")]
    Guid BroadcastId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-broadcast-audience")]
    NotificationBroadcastAudience Audience,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-broadcast-message")]
    OperationsNotificationBroadcastMessageTenantExport Message);

internal sealed record OperationsNotificationBroadcastReadTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-broadcast-read-id")]
    Guid ReadId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-broadcast-id")]
    Guid BroadcastId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-recipient-kind")]
    NotificationBroadcastRecipientKind RecipientKind,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-recipient-id")]
    string RecipientId,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-read-at")]
    DateTimeOffset ReadAtUtc);

internal sealed record OperationsNotificationHistoryReferenceStateTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-history-reference")]
    OperationsNotificationHistoryReferenceTenantExport Reference,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-history-lifecycle-proof")]
    OperationsNotificationHistoryStateProofTenantExport Proof);

internal sealed record OperationsNotificationHistoryCloseReceiptTenantExport(
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-history-reference")]
    OperationsNotificationHistoryReferenceTenantExport Reference,
    [property: OperationsNotificationsTenantExportField(
        "operations-notifications.tenant-export-history-lifecycle-proof")]
    OperationsNotificationHistoryCloseProofTenantExport Proof);

internal sealed record
    OperationsNotificationHistoryBatchCloseOperationTenantExport(
        [property: OperationsNotificationsTenantExportField(
            "operations-notifications.tenant-export-history-reference")]
        OperationsNotificationHistoryReferenceTenantExport Reference,
        [property: OperationsNotificationsTenantExportField(
            "operations-notifications.tenant-export-history-lifecycle-proof")]
        OperationsNotificationHistoryBatchOperationProofTenantExport Proof);

internal sealed record
    OperationsNotificationHistoryBatchCloseReceiptTenantExport(
        [property: OperationsNotificationsTenantExportField(
            "operations-notifications.tenant-export-history-reference")]
        OperationsNotificationHistoryReferenceTenantExport Reference,
        [property: OperationsNotificationsTenantExportField(
            "operations-notifications.tenant-export-history-lifecycle-proof")]
        OperationsNotificationHistoryBatchReceiptProofTenantExport Proof);

internal sealed record OperationsNotificationHistoryReferenceTenantExport(
    string Namespace,
    string Digest);

internal sealed record OperationsNotificationPreferenceStateTenantExport(
    string TagKey,
    bool Enabled,
    int Version,
    DateTimeOffset UpdatedAtUtc);

internal sealed record OperationsNotificationDeliveryRouteStateTenantExport(
    string DeliveryTag,
    NotificationDeliveryProviderCode Provider,
    bool IsActive,
    int Version,
    DateTimeOffset UpdatedAtUtc);

internal sealed record OperationsNotificationTagDefinitionStateTenantExport(
    string TagKey,
    NotificationTagKind Kind,
    string DisplayName,
    string Description,
    NotificationTagOrigin Origin,
    string Owner,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record OperationsNotificationDeliveryStateTenantExport(
    string DeliveryTag,
    NotificationDeliveryProviderCode Provider,
    NotificationDeliveryStatus Status,
    int Attempts,
    int MaxAttempts,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    string? LockedBy,
    DateTimeOffset? LockedUntilUtc,
    DateTimeOffset? DeliveredAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? LastCode,
    string? ProviderMessageId,
    Guid ConcurrencyStamp);

internal sealed record OperationsNotificationDeliveryAttemptStateTenantExport(
    int AttemptNumber,
    NotificationDeliveryProviderCode Provider,
    NotificationDeliveryAttemptOutcome Outcome,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? Code,
    string? ProviderMessageId);

internal sealed record OperationsNotificationBroadcastMessageTenantExport(
    string SourceModule,
    string NotificationName,
    int NotificationVersion,
    string Title,
    string? Body,
    NotificationSeverity Severity,
    long StreamSequence,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    JsonElement Payload);

internal sealed record OperationsNotificationHistoryStateProofTenantExport(
    long Version,
    bool IsClosed,
    DateTimeOffset? ClosedAtUtc,
    Guid? CloseOperationId,
    string? CloseRequestSha256);

internal sealed record OperationsNotificationHistoryCloseProofTenantExport(
    string RequestSha256,
    long ResultingVersion,
    int RemovedRecordCount,
    string RemovedRecordIdsSha256,
    DateTimeOffset CompletedAtUtc);

internal sealed record
    OperationsNotificationHistoryBatchOperationProofTenantExport(
        string RequestSha256,
        long ExpectedVersion,
        long ResultingVersion,
        int BatchSize,
        long RemovedRecordCount,
        int CompletedBatchCount,
        int RemovalProofVersion,
        string RemovalProofSha256,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset UpdatedAtUtc);

internal sealed record
    OperationsNotificationHistoryBatchReceiptProofTenantExport(
        string RequestSha256,
        long ResultingVersion,
        long RemovedRecordCount,
        int CompletedBatchCount,
        int RemovalProofVersion,
        string RemovalProofSha256,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc);
