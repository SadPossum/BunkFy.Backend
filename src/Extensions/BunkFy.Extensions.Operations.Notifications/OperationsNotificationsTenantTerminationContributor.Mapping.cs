namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;

internal sealed partial class OperationsNotificationsTenantTerminationContributor
{
    private static readonly Guid HistoryReferenceStateRecordNamespace =
        Guid.Parse("f2920df0-ec09-4aa9-bab4-6d5aaf04bed1");

    private static DataRightsExportRecord Map(
        NotificationScopeExportStore store,
        NotificationScopeExportRecord record)
    {
        if (!OperationsNotificationsTenantExportRecordValidator.IsValid(
                store,
                record))
        {
            throw new InvalidDataException(
                "The Operations Notifications scope-export record is invalid.");
        }

        return (store, record) switch
        {
            (NotificationScopeExportStore.UserNotifications,
                NotificationScopeUserNotificationExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .NotificationRecordType,
                    value.NotificationId,
                    value.StreamSequence,
                    new OperationsNotificationTenantExport(
                        value.NotificationId,
                        value.RecipientId,
                        value.SourceModule,
                        value.NotificationName,
                        value.NotificationVersion,
                        value.Title,
                        value.Body,
                        value.Severity,
                        value.StreamSequence,
                        value.OccurredAtUtc,
                        value.CreatedAtUtc,
                        value.ReadAtUtc,
                        value.Payload.Clone(),
                        value.DeliveryPolicy,
                        value.IsInboxVisible,
                        value.Tags.ToArray(),
                        value.References.Select(MapReference).ToArray())),
            (NotificationScopeExportStore.Preferences,
                NotificationScopePreferenceExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .PreferenceRecordType,
                    value.PreferenceId,
                    value.Version,
                    new OperationsNotificationPreferenceTenantExport(
                        value.PreferenceId,
                        value.UserId,
                        new OperationsNotificationPreferenceStateTenantExport(
                            value.TagKey,
                            value.Enabled,
                            value.Version,
                            value.UpdatedAtUtc))),
            (NotificationScopeExportStore.DeliveryRoutes,
                NotificationScopeDeliveryRouteExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .DeliveryRouteRecordType,
                    value.RouteId,
                    value.Version,
                    new OperationsNotificationDeliveryRouteTenantExport(
                        value.RouteId,
                        new OperationsNotificationDeliveryRouteStateTenantExport(
                            value.DeliveryTag,
                            value.Provider,
                            value.IsActive,
                            value.Version,
                            value.UpdatedAtUtc),
                        value.UpdatedBy)),
            (NotificationScopeExportStore.TagDefinitions,
                NotificationScopeTagDefinitionExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .TagDefinitionRecordType,
                    value.DefinitionId,
                    value.Version,
                    new OperationsNotificationTagDefinitionTenantExport(
                        value.DefinitionId,
                        new OperationsNotificationTagDefinitionStateTenantExport(
                            value.TagKey,
                            value.Kind,
                            value.DisplayName,
                            value.Description,
                            value.Origin,
                            value.Owner,
                            value.IsActive,
                            value.Version,
                            value.CreatedAtUtc,
                            value.UpdatedAtUtc),
                        value.CreatedBy,
                        value.UpdatedBy)),
            (NotificationScopeExportStore.Deliveries,
                NotificationScopeDeliveryExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .DeliveryRecordType,
                    value.DeliveryId,
                    recordVersion: 1,
                    new OperationsNotificationDeliveryTenantExport(
                        value.DeliveryId,
                        value.NotificationId,
                        new OperationsNotificationDeliveryStateTenantExport(
                            value.DeliveryTag,
                            value.Provider,
                            value.Status,
                            value.Attempts,
                            value.MaxAttempts,
                            value.CreatedAtUtc,
                            value.NextAttemptAtUtc,
                            value.LockedBy,
                            value.LockedUntilUtc,
                            value.DeliveredAtUtc,
                            value.CompletedAtUtc,
                            value.LastCode,
                            value.ProviderMessageId,
                            value.ConcurrencyStamp))),
            (NotificationScopeExportStore.DeliveryAttempts,
                NotificationScopeDeliveryAttemptExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .DeliveryAttemptRecordType,
                    value.AttemptId,
                    value.AttemptNumber,
                    new OperationsNotificationDeliveryAttemptTenantExport(
                        value.AttemptId,
                        value.DeliveryId,
                        new OperationsNotificationDeliveryAttemptStateTenantExport(
                            value.AttemptNumber,
                            value.Provider,
                            value.Outcome,
                            value.StartedAtUtc,
                            value.CompletedAtUtc,
                            value.Code,
                            value.ProviderMessageId))),
            (NotificationScopeExportStore.TenantBroadcasts,
                NotificationScopeBroadcastExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .BroadcastRecordType,
                    value.BroadcastId,
                    value.StreamSequence,
                    new OperationsNotificationBroadcastTenantExport(
                        value.BroadcastId,
                        value.Audience,
                        new OperationsNotificationBroadcastMessageTenantExport(
                            value.SourceModule,
                            value.NotificationName,
                            value.NotificationVersion,
                            value.Title,
                            value.Body,
                            value.Severity,
                            value.StreamSequence,
                            value.OccurredAtUtc,
                            value.CreatedAtUtc,
                            value.Payload.Clone()))),
            (NotificationScopeExportStore.TenantBroadcastReads,
                NotificationScopeBroadcastReadExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .BroadcastReadRecordType,
                    value.ReadId,
                    recordVersion: 1,
                    new OperationsNotificationBroadcastReadTenantExport(
                        value.ReadId,
                        value.BroadcastId,
                        value.RecipientKind,
                        value.RecipientId,
                        value.ReadAtUtc)),
            (NotificationScopeExportStore.HistoryReferenceStates,
                NotificationScopeHistoryReferenceStateExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .HistoryReferenceStateRecordType,
                    HistoryReferenceStateId(value.Reference),
                    value.Version,
                    new OperationsNotificationHistoryReferenceStateTenantExport(
                        MapReference(value.Reference),
                        new OperationsNotificationHistoryStateProofTenantExport(
                            value.Version,
                            value.IsClosed,
                            value.ClosedAtUtc,
                            value.CloseOperationId,
                            value.CloseRequestSha256))),
            (NotificationScopeExportStore.HistoryCloseReceipts,
                NotificationScopeHistoryCloseReceiptExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .HistoryCloseReceiptRecordType,
                    value.OperationId,
                    value.ResultingVersion,
                    new OperationsNotificationHistoryCloseReceiptTenantExport(
                        MapReference(value.Reference),
                        new OperationsNotificationHistoryCloseProofTenantExport(
                            value.RequestSha256,
                            value.ResultingVersion,
                            value.RemovedRecordCount,
                            value.RemovedRecordIdsSha256,
                            value.CompletedAtUtc))),
            (NotificationScopeExportStore.HistoryBatchCloseOperations,
                NotificationScopeHistoryBatchCloseOperationExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .HistoryBatchCloseOperationRecordType,
                    value.OperationId,
                    value.ResultingVersion,
                    new OperationsNotificationHistoryBatchCloseOperationTenantExport(
                        MapReference(value.Reference),
                        new OperationsNotificationHistoryBatchOperationProofTenantExport(
                            value.RequestSha256,
                            value.ExpectedVersion,
                            value.ResultingVersion,
                            value.BatchSize,
                            value.RemovedRecordCount,
                            value.CompletedBatchCount,
                            value.RemovalProofVersion,
                            value.RemovalProofSha256,
                            value.StartedAtUtc,
                            value.UpdatedAtUtc))),
            (NotificationScopeExportStore.HistoryBatchCloseReceipts,
                NotificationScopeHistoryBatchCloseReceiptExportRecord value) =>
                Create(
                    OperationsNotificationsTenantTerminationMetadata
                        .HistoryBatchCloseReceiptRecordType,
                    value.OperationId,
                    value.ResultingVersion,
                    new OperationsNotificationHistoryBatchCloseReceiptTenantExport(
                        MapReference(value.Reference),
                        new OperationsNotificationHistoryBatchReceiptProofTenantExport(
                            value.RequestSha256,
                            value.ResultingVersion,
                            value.RemovedRecordCount,
                            value.CompletedBatchCount,
                            value.RemovalProofVersion,
                            value.RemovalProofSha256,
                            value.StartedAtUtc,
                            value.CompletedAtUtc))),
            _ => throw new InvalidDataException(
                "The Operations Notifications scope-export record type is invalid.")
        };
    }

    private static DataRightsExportRecord Create(
        string recordType,
        Guid recordId,
        long recordVersion,
        object source) =>
        OperationsNotificationsTenantTerminationExportSchema.CreateRecord(
            recordType,
            recordId,
            recordVersion,
            source);

    private static OperationsNotificationHistoryReferenceTenantExport
        MapReference(NotificationHistoryReference reference) =>
        new(reference.Namespace, reference.Digest);

    private static Guid HistoryReferenceStateId(
        NotificationHistoryReference reference) =>
        DataRightsExportRecordIds.CreateDeterministicChild(
            HistoryReferenceStateRecordNamespace,
            reference.Namespace + ':' + reference.Digest);
}
