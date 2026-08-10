namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Contracts;

internal sealed class
    OperationsNotificationsDataRightsAnonymisationRestoreContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ISystemClock clock)
    : OperationsNotificationsGuestHistoryAnonymisationRestoreContributor(
        lifecycle,
        scopeContext,
        clock)
{
    public override string RecordType =>
        OperationsNotificationsDataRightsCoordinates
            .ReservationHistoryRecordType;

    protected override NotificationHistoryReference CreateReference(
        DataRightsAnonymisationRestoreRequest request) =>
        OperationsNotificationsDataRightsCoordinates.ForReservation(
            request.TenantId,
            request.RoutingPropertyId,
            request.RecordId);
}
