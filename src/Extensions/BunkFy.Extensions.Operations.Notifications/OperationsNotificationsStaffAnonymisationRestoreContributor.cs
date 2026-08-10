namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Contracts;

internal sealed class
    OperationsNotificationsStaffAnonymisationRestoreContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ISystemClock clock)
    : IDataRightsAnonymisationRestoreContributorV3
{
    private const string InvalidRequest =
        "OperationsNotifications.StaffAnonymisationRestoreRequestInvalid";
    private const string Failed =
        "OperationsNotifications.StaffAnonymisationRestoreFailed";
    private const string Busy =
        "OperationsNotifications.StaffAnonymisationRestoreDeliveryBusy";

    public string OwnerKey =>
        OperationsNotificationsDataRightsCoordinates.Owner;

    public string RecordType =>
        OperationsNotificationsDataRightsCoordinates
            .StaffInboxHistoryRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public int ContractVersion =>
        DataRightsAnonymisationRestoreContractV3.CurrentVersion;

    public async Task<DataRightsAnonymisationRestoreResult> RestoreAsync(
        DataRightsAnonymisationRestoreRequestV3 request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return DataRightsAnonymisationRestoreResult.Failed(
                this.ContractVersion,
                InvalidRequest);
        }

        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates.ForStaff(
                request.TenantId,
                request.RecordId);
        long selectedVersion = request.ResultingRecordVersion - 1;
        NotificationHistoryReferenceCloseResult result =
            await lifecycle.CloseAsync(
                    new NotificationHistoryReferenceCloseRequest(
                        request.OwnerReceiptId,
                        request.TenantId,
                        reference,
                        selectedVersion,
                        OperationsNotificationsDataRightsReceipt
                            .StaffMaximumRecords),
                    cancellationToken)
                .ConfigureAwait(false);
        if (result.Status is
                NotificationHistoryReferenceCloseStatus.Completed or
                NotificationHistoryReferenceCloseStatus.Replayed &&
            OperationsNotificationsDataRightsReceipt.IsValid(
                result.Receipt,
                request.OwnerReceiptId,
                reference,
                selectedVersion) &&
            string.Equals(
                OperationsNotificationsDataRightsReceipt.ComputeSha256(
                    result.Receipt!),
                request.OwnerReceiptSha256,
                StringComparison.Ordinal))
        {
            NotificationHistoryReferenceCloseReceipt receipt =
                result.Receipt!;
            return DataRightsAnonymisationRestoreResult.Completed(
                this.ContractVersion,
                new DataRightsAnonymisationRestoreProof(
                    request.LedgerEntryId,
                    request.OwnerReceiptId,
                    request.OwnerReceiptSha256,
                    receipt.ResultingVersion,
                    receipt.ResultingVersion,
                    clock.UtcNow));
        }

        if (result.Status ==
            NotificationHistoryReferenceCloseStatus.Busy)
        {
            throw new InvalidOperationException(Busy);
        }

        return DataRightsAnonymisationRestoreResult.Failed(
            this.ContractVersion,
            Failed);
    }

    private bool IsValid(
        DataRightsAnonymisationRestoreRequestV3? request) =>
        request is not null &&
        request.ContractVersion == this.ContractVersion &&
        request.LedgerEntryId != Guid.Empty &&
        request.TenantSequence > 0 &&
        OperationsNotificationsDataRightsValidation.IsSha256(
            request.LedgerEntrySha256) &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        OperationsNotificationsDataRightsValidation.IsStaffTenantScope(
            scopeContext,
            request.TenantId,
            request.CaseType,
            request.RoutingPropertyId) &&
        string.Equals(
            request.OwnerKey,
            this.OwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            this.RecordType,
            StringComparison.Ordinal) &&
        request.RecordId != Guid.Empty &&
        request.OwnerReceiptContractVersion ==
            OperationsNotificationsDataRightsReceipt.ContractVersion &&
        request.OwnerReceiptId != Guid.Empty &&
        OperationsNotificationsDataRightsValidation.IsSha256(
            request.OwnerReceiptSha256) &&
        request.ResultingRecordVersion > 1 &&
        request.OriginallyCompletedAtUtc != default;
}
