namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.Logging;

internal sealed class
    OperationsNotificationsReservationAccessExportCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger<
            OperationsNotificationsReservationAccessExportCompanionContributor>
            logger)
    : OperationsNotificationsReservationRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string ContributorKey =>
        OperationsNotificationsDataRightsCoordinates
            .ReservationAccessExportCompanionKey;

    public override DataRightsOperation Operation =>
        DataRightsOperation.AccessExport;

}

internal sealed class
    OperationsNotificationsReservationAnonymisationCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger<
            OperationsNotificationsReservationAnonymisationCompanionContributor>
            logger)
    : OperationsNotificationsReservationRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string ContributorKey =>
        OperationsNotificationsDataRightsCoordinates
            .ReservationAnonymisationCompanionKey;

    public override DataRightsOperation Operation =>
        DataRightsOperation.Anonymisation;

}

internal abstract class
    OperationsNotificationsReservationRequiredCompanionContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ILogger logger)
    : OperationsNotificationsGuestHistoryRequiredCompanionContributor(
        lifecycle,
        scopeContext,
        logger)
{
    public override string SourceOwnerKey =>
        ReservationsDataRightsCoordinates.Owner;

    public override string SourceRecordType =>
        ReservationsDataRightsCoordinates.ReservationRecordType;

    protected override string TargetRecordType =>
        OperationsNotificationsDataRightsCoordinates
            .ReservationHistoryRecordType;

    protected override NotificationHistoryReference CreateReference(
        DataRightsRequiredCompanionRequest request) =>
        OperationsNotificationsDataRightsCoordinates.ForReservation(
            request.TenantId,
            request.PropertyId!.Value,
            request.SourceCoordinate.RecordId);
}
