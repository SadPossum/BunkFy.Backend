namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;

internal sealed class
    OperationsNotificationsDataRightsAnonymisationContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ISystemClock clock)
    : IDataRightsAnonymisationContributor
{
    private const string InvalidRequest =
        "OperationsNotifications.AnonymisationRequestInvalid";
    private const string Stale =
        "OperationsNotifications.AnonymisationHistoryStale";
    private const string Overflow =
        "OperationsNotifications.AnonymisationHistoryOversized";
    private const string Failed =
        "OperationsNotifications.AnonymisationFailed";
    private const string Busy =
        "OperationsNotifications.AnonymisationDeliveryBusy";

    public string OwnerKey =>
        OperationsNotificationsDataRightsCoordinates.Owner;

    public int ContractVersion =>
        DataRightsAnonymisationContract.CurrentVersion;

    public async Task<DataRightsAnonymisationContributionResult>
        ExecuteAsync(
            DataRightsAnonymisationContributionRequest request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                InvalidRequest);
        }

        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                request.TenantId,
                request.RoutingPropertyId,
                request.Coordinate.RecordId);
        NotificationHistoryReferenceCloseResult result =
            await lifecycle.CloseAsync(
                    new NotificationHistoryReferenceCloseRequest(
                        request.WorkItemId,
                        request.TenantId,
                        reference,
                        request.Coordinate.RecordVersion,
                        OperationsNotificationsDataRightsReceipt
                            .MaximumRecords),
                    cancellationToken)
                .ConfigureAwait(false);
        if (result.Status is
                NotificationHistoryReferenceCloseStatus.Completed or
                NotificationHistoryReferenceCloseStatus.Replayed &&
            OperationsNotificationsDataRightsReceipt.IsValid(
                result.Receipt,
                request.WorkItemId,
                reference,
                request.Coordinate.RecordVersion))
        {
            NotificationHistoryReferenceCloseReceipt receipt =
                result.Receipt!;
            return DataRightsAnonymisationContributionResult.Completed(
                new DataRightsAnonymisationOwnerProof(
                    OperationsNotificationsDataRightsReceipt.ContractVersion,
                    receipt.OperationId,
                    receipt.ResultingVersion,
                    OperationsNotificationsDataRightsReceipt.DispositionCode,
                    OperationsNotificationsDataRightsReceipt.ReasonCode,
                    OperationsNotificationsDataRightsReceipt.ComputeSha256(
                        receipt),
                    receipt.CompletedAtUtc));
        }

        return result.Status switch
        {
            NotificationHistoryReferenceCloseStatus.Busy =>
                throw new InvalidOperationException(Busy),
            NotificationHistoryReferenceCloseStatus.Stale =>
                DataRightsAnonymisationContributionResult.Blocked(Stale),
            NotificationHistoryReferenceCloseStatus.Overflow =>
                DataRightsAnonymisationContributionResult.Blocked(Overflow),
            _ => DataRightsAnonymisationContributionResult.Failed(Failed)
        };
    }

    private bool IsValid(
        DataRightsAnonymisationContributionRequest? request) =>
        request is not null &&
        request.ContractVersion == this.ContractVersion &&
        request.WorkItemId != Guid.Empty &&
        request.IdempotencyKey != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > 0 &&
        request.DeadlineUtc > clock.UtcNow &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        OperationsNotificationsDataRightsValidation.IsGuestPropertyScope(
            scopeContext,
            request.TenantId,
            DataRightsCaseType.GuestRights,
            request.RoutingPropertyId) &&
        OperationsNotificationsDataRightsValidation
            .IsReservationHistoryCoordinate(request.Coordinate) &&
        OperationsNotificationsDataRightsValidation.IsApprovalEvidence(
            request.RoutingPolicy,
            request.RoutingPropertyId);
}
