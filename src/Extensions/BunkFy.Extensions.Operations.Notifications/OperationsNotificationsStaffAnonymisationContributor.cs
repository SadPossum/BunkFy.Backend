namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Contracts;

internal sealed class
    OperationsNotificationsStaffAnonymisationContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ISystemClock clock)
    : IDataRightsAnonymisationContributorV2
{
    private const string InvalidRequest =
        "OperationsNotifications.StaffAnonymisationRequestInvalid";
    private const string Stale =
        "OperationsNotifications.StaffAnonymisationHistoryStale";
    private const string Overflow =
        "OperationsNotifications.StaffAnonymisationHistoryOversized";
    private const string Failed =
        "OperationsNotifications.StaffAnonymisationFailed";
    private const string Busy =
        "OperationsNotifications.StaffAnonymisationDeliveryBusy";

    public string OwnerKey =>
        OperationsNotificationsDataRightsCoordinates.Owner;

    public string RecordType =>
        OperationsNotificationsDataRightsCoordinates
            .StaffInboxHistoryRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public int ContractVersion =>
        DataRightsAnonymisationContractV2.CurrentVersion;

    public async Task<DataRightsAnonymisationContributionResult>
        ExecuteAsync(
            DataRightsAnonymisationContributionRequestV2 request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return DataRightsAnonymisationContributionResult.Failed(
                this.ContractVersion,
                InvalidRequest);
        }

        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates.ForStaff(
                request.TenantId,
                request.Coordinate.RecordId);
        NotificationHistoryReferenceCloseResult result =
            await lifecycle.CloseAsync(
                    new NotificationHistoryReferenceCloseRequest(
                        request.WorkItemId,
                        request.TenantId,
                        reference,
                        request.Coordinate.RecordVersion,
                        OperationsNotificationsDataRightsReceipt
                            .StaffMaximumRecords),
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
                this.ContractVersion,
                new DataRightsAnonymisationOwnerProof(
                    OperationsNotificationsDataRightsReceipt.ContractVersion,
                    receipt.OperationId,
                    receipt.ResultingVersion,
                    OperationsNotificationsDataRightsReceipt.DispositionCode,
                    OperationsNotificationsDataRightsReceipt.StaffReasonCode,
                    OperationsNotificationsDataRightsReceipt.ComputeSha256(
                        receipt),
                    receipt.CompletedAtUtc));
        }

        return result.Status switch
        {
            NotificationHistoryReferenceCloseStatus.Busy =>
                throw new InvalidOperationException(Busy),
            NotificationHistoryReferenceCloseStatus.Stale =>
                DataRightsAnonymisationContributionResult.Blocked(
                    this.ContractVersion,
                    Stale),
            NotificationHistoryReferenceCloseStatus.Overflow =>
                DataRightsAnonymisationContributionResult.Blocked(
                    this.ContractVersion,
                    Overflow),
            _ => DataRightsAnonymisationContributionResult.Failed(
                this.ContractVersion,
                Failed)
        };
    }

    private bool IsValid(
        DataRightsAnonymisationContributionRequestV2? request) =>
        request is not null &&
        request.ContractVersion == this.ContractVersion &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        request.WorkItemId != Guid.Empty &&
        request.IdempotencyKey != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > 0 &&
        request.DeadlineUtc > clock.UtcNow &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        OperationsNotificationsDataRightsValidation.IsStaffTenantScope(
            scopeContext,
            request.TenantId,
            request.CaseType,
            request.PropertyId) &&
        OperationsNotificationsDataRightsValidation
            .IsStaffHistoryCoordinate(request.Coordinate) &&
        OperationsNotificationsDataRightsValidation
            .IsStaffApprovalEvidence(request.ApprovalEvidence);
}
