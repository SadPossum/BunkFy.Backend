namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.Logging;

internal sealed class
    OperationsNotificationsIngestionAccessExportCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger<
            OperationsNotificationsIngestionAccessExportCompanionContributor>
            logger)
    : OperationsNotificationsIngestionRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string ContributorKey =>
        OperationsNotificationsDataRightsCoordinates
            .IngestionAccessExportCompanionKey;

    public override DataRightsOperation Operation =>
        DataRightsOperation.AccessExport;
}

internal sealed class
    OperationsNotificationsIngestionAnonymisationCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger<
            OperationsNotificationsIngestionAnonymisationCompanionContributor>
            logger)
    : OperationsNotificationsIngestionRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string ContributorKey =>
        OperationsNotificationsDataRightsCoordinates
            .IngestionAnonymisationCompanionKey;

    public override DataRightsOperation Operation =>
        DataRightsOperation.Anonymisation;
}

internal abstract class
    OperationsNotificationsIngestionRequiredCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger logger)
    : OperationsNotificationsGuestHistoryRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string SourceOwnerKey =>
        IngestionDataRightsCoordinates.Owner;

    public override string SourceRecordType =>
        IngestionDataRightsCoordinates.ReservationSourceLinkRecordType;

    protected override string TargetRecordType =>
        OperationsNotificationsDataRightsCoordinates
            .IngestionSourceLinkHistoryRecordType;

    protected override NotificationHistoryReference CreateReference(
        DataRightsRequiredCompanionRequest request) =>
        OperationsNotificationsDataRightsCoordinates
            .ForIngestionSourceLink(
                request.TenantId,
                request.PropertyId!.Value,
                request.SourceCoordinate.RecordId);
}
