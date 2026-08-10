namespace BunkFy.Extensions.Operations.Notifications;

using System.Text.Json;
using Gma.Modules.Notifications.Contracts;

internal static class OperationsNotificationsTenantExportRecordValidator
{
    public static bool IsValid(
        NotificationScopeExportStore store,
        NotificationScopeExportRecord? record) =>
        (store, record) switch
        {
            (NotificationScopeExportStore.UserNotifications,
                NotificationScopeUserNotificationExportRecord value) =>
                IsValid(value),
            (NotificationScopeExportStore.Preferences,
                NotificationScopePreferenceExportRecord value) =>
                value.PreferenceId != Guid.Empty &&
                IsText(value.UserId) &&
                IsText(value.TagKey) &&
                value.Version > 0 &&
                value.UpdatedAtUtc != default,
            (NotificationScopeExportStore.DeliveryRoutes,
                NotificationScopeDeliveryRouteExportRecord value) =>
                value.RouteId != Guid.Empty &&
                IsText(value.DeliveryTag) &&
                IsProvider(value.Provider) &&
                value.Version > 0 &&
                value.UpdatedAtUtc != default &&
                IsText(value.UpdatedBy),
            (NotificationScopeExportStore.TagDefinitions,
                NotificationScopeTagDefinitionExportRecord value) =>
                value.DefinitionId != Guid.Empty &&
                IsText(value.TagKey) &&
                IsDefined(value.Kind) &&
                IsText(value.DisplayName) &&
                IsText(value.Description, allowEmpty: true) &&
                IsDefined(value.Origin) &&
                IsText(value.Owner) &&
                value.Version > 0 &&
                value.CreatedAtUtc != default &&
                value.UpdatedAtUtc >= value.CreatedAtUtc &&
                IsText(value.CreatedBy) &&
                IsText(value.UpdatedBy),
            (NotificationScopeExportStore.Deliveries,
                NotificationScopeDeliveryExportRecord value) =>
                value.DeliveryId != Guid.Empty &&
                value.NotificationId != Guid.Empty &&
                IsText(value.DeliveryTag) &&
                IsProvider(value.Provider) &&
                IsDefined(value.Status) &&
                value.Attempts >= 0 &&
                value.MaxAttempts > 0 &&
                value.Attempts <= value.MaxAttempts &&
                value.CreatedAtUtc != default &&
                value.ConcurrencyStamp != Guid.Empty,
            (NotificationScopeExportStore.DeliveryAttempts,
                NotificationScopeDeliveryAttemptExportRecord value) =>
                value.AttemptId != Guid.Empty &&
                value.DeliveryId != Guid.Empty &&
                value.AttemptNumber > 0 &&
                IsProvider(value.Provider) &&
                IsDefined(value.Outcome) &&
                value.StartedAtUtc != default &&
                value.CompletedAtUtc >= value.StartedAtUtc,
            (NotificationScopeExportStore.TenantBroadcasts,
                NotificationScopeBroadcastExportRecord value) =>
                value.BroadcastId != Guid.Empty &&
                value.Audience is NotificationBroadcastAudience.TenantUsers or
                    NotificationBroadcastAudience.TenantAdmins &&
                IsNotificationMessage(
                    value.SourceModule,
                    value.NotificationName,
                    value.NotificationVersion,
                    value.Title,
                    value.Severity,
                    value.StreamSequence,
                    value.OccurredAtUtc,
                    value.CreatedAtUtc,
                    value.Payload),
            (NotificationScopeExportStore.TenantBroadcastReads,
                NotificationScopeBroadcastReadExportRecord value) =>
                value.ReadId != Guid.Empty &&
                value.BroadcastId != Guid.Empty &&
                IsDefined(value.RecipientKind) &&
                IsText(value.RecipientId) &&
                value.ReadAtUtc != default,
            (NotificationScopeExportStore.HistoryReferenceStates,
                NotificationScopeHistoryReferenceStateExportRecord value) =>
                IsReference(value.Reference) &&
                value.Version > 0 &&
                (value.IsClosed
                    ? value.ClosedAtUtc.HasValue &&
                      value.ClosedAtUtc.Value != default &&
                      value.CloseOperationId.HasValue &&
                      value.CloseOperationId.Value != Guid.Empty &&
                      IsSha256(value.CloseRequestSha256)
                    : !value.ClosedAtUtc.HasValue &&
                      !value.CloseOperationId.HasValue &&
                      value.CloseRequestSha256 is null),
            (NotificationScopeExportStore.HistoryCloseReceipts,
                NotificationScopeHistoryCloseReceiptExportRecord value) =>
                value.OperationId != Guid.Empty &&
                IsReference(value.Reference) &&
                IsSha256(value.RequestSha256) &&
                value.ResultingVersion > 0 &&
                value.RemovedRecordCount >= 0 &&
                IsSha256(value.RemovedRecordIdsSha256) &&
                value.CompletedAtUtc != default,
            (NotificationScopeExportStore.HistoryBatchCloseOperations,
                NotificationScopeHistoryBatchCloseOperationExportRecord value) =>
                value.OperationId != Guid.Empty &&
                IsReference(value.Reference) &&
                IsSha256(value.RequestSha256) &&
                value.ExpectedVersion >= 0 &&
                value.ResultingVersion == value.ExpectedVersion + 1 &&
                value.BatchSize is > 0 and <=
                    NotificationHistoryLifecycleLimits.MaximumCloseBatchSize &&
                value.RemovedRecordCount >= 0 &&
                value.CompletedBatchCount >= 0 &&
                value.RemovalProofVersion > 0 &&
                IsSha256(value.RemovalProofSha256) &&
                value.StartedAtUtc != default &&
                value.UpdatedAtUtc >= value.StartedAtUtc,
            (NotificationScopeExportStore.HistoryBatchCloseReceipts,
                NotificationScopeHistoryBatchCloseReceiptExportRecord value) =>
                value.OperationId != Guid.Empty &&
                IsReference(value.Reference) &&
                IsSha256(value.RequestSha256) &&
                value.ResultingVersion > 0 &&
                value.RemovedRecordCount >= 0 &&
                value.CompletedBatchCount >= 0 &&
                value.RemovalProofVersion > 0 &&
                IsSha256(value.RemovalProofSha256) &&
                value.StartedAtUtc != default &&
                value.CompletedAtUtc >= value.StartedAtUtc,
            _ => false
        };

    private static bool IsValid(
        NotificationScopeUserNotificationExportRecord value) =>
        value.NotificationId != Guid.Empty &&
        IsText(value.RecipientId) &&
        IsNotificationMessage(
            value.SourceModule,
            value.NotificationName,
            value.NotificationVersion,
            value.Title,
            value.Severity,
            value.StreamSequence,
            value.OccurredAtUtc,
            value.CreatedAtUtc,
            value.Payload) &&
        IsDefined(value.DeliveryPolicy) &&
        value.Tags is not null &&
        value.Tags.Count <= 32 &&
        value.Tags.All(tag => IsText(tag)) &&
        value.References is not null &&
        value.References.Count <= NotificationHistoryReference.MaxCount &&
        value.References.All(IsReference) &&
        value.References.Distinct().Count() == value.References.Count;

    private static bool IsNotificationMessage(
        string sourceModule,
        string notificationName,
        int notificationVersion,
        string title,
        NotificationSeverity severity,
        long streamSequence,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset createdAtUtc,
        JsonElement payload) =>
        IsText(sourceModule) &&
        IsText(notificationName) &&
        notificationVersion > 0 &&
        IsText(title) &&
        IsDefined(severity) &&
        streamSequence > 0 &&
        occurredAtUtc != default &&
        createdAtUtc != default &&
        payload.ValueKind != JsonValueKind.Undefined;

    private static bool IsReference(NotificationHistoryReference? reference) =>
        reference is not null &&
        IsText(reference.Namespace) &&
        IsSha256(reference.Digest);

    private static bool IsProvider(NotificationDeliveryProviderCode? provider) =>
        provider is not null && IsText(provider.Value);

    private static bool IsSha256(string? value) =>
        OperationsNotificationsDataRightsValidation.IsSha256(value);

    private static bool IsText(string? value, bool allowEmpty = false)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return (allowEmpty || normalized.Length > 0) &&
            normalized.Length <= DataRightsFieldTextLimit &&
            !normalized.Any(char.IsControl);
    }

    private static bool IsDefined<T>(T value)
        where T : struct, Enum =>
        Enum.IsDefined(value) &&
        !EqualityComparer<T>.Default.Equals(value, default);

    private const int DataRightsFieldTextLimit = 4_096;
}
