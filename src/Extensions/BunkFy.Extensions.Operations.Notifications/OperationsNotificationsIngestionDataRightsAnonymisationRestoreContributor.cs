namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Contracts;

internal sealed class
    OperationsNotificationsIngestionDataRightsAnonymisationRestoreContributor(
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
            .IngestionSourceLinkHistoryRecordType;

    protected override NotificationHistoryReference CreateReference(
        DataRightsAnonymisationRestoreRequest request) =>
        OperationsNotificationsDataRightsCoordinates.ForIngestionSourceLink(
            request.TenantId,
            request.RoutingPropertyId,
            request.RecordId);
}
