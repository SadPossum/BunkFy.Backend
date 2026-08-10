namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Contracts;

internal abstract class
    OperationsNotificationsGuestHistoryAnonymisationRestoreContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext,
        ISystemClock clock)
    : IDataRightsAnonymisationRestoreContributor
{
    private const string InvalidRequest =
        "OperationsNotifications.AnonymisationRestoreRequestInvalid";
    private const string Failed =
        "OperationsNotifications.AnonymisationRestoreFailed";
    private const string Busy =
        "OperationsNotifications.AnonymisationRestoreDeliveryBusy";

    public string OwnerKey =>
        OperationsNotificationsDataRightsCoordinates.Owner;

    public abstract string RecordType { get; }

    public int ContractVersion =>
        DataRightsAnonymisationRestoreContract.CurrentVersion;

    public async Task<DataRightsAnonymisationRestoreResult> RestoreAsync(
        DataRightsAnonymisationRestoreRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return DataRightsAnonymisationRestoreResult.Failed(
                InvalidRequest);
        }

        NotificationHistoryReference reference =
            this.CreateReference(request);
        long selectedVersion =
            request.ResultingRecordVersion!.Value - 1;
        NotificationHistoryReferenceCloseResult result =
            await lifecycle.CloseAsync(
                    new NotificationHistoryReferenceCloseRequest(
                        request.OwnerReceiptId,
                        request.TenantId,
                        reference,
                        selectedVersion,
                        OperationsNotificationsDataRightsReceipt
                            .MaximumRecords),
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
                new DataRightsAnonymisationRestoreProof(
                    request.LedgerEntryId,
                    request.OwnerReceiptId,
                    request.OwnerReceiptSha256,
                    receipt.ResultingVersion,
                    receipt.ResultingVersion,
                    clock.UtcNow));
        }

        if (result.Status == NotificationHistoryReferenceCloseStatus.Busy)
        {
            throw new InvalidOperationException(Busy);
        }

        return DataRightsAnonymisationRestoreResult.Failed(Failed);
    }

    protected abstract NotificationHistoryReference CreateReference(
        DataRightsAnonymisationRestoreRequest request);

    private bool IsValid(
        DataRightsAnonymisationRestoreRequest? request) =>
        request is not null &&
        request.ContractVersion == this.ContractVersion &&
        request.LedgerEntryId != Guid.Empty &&
        request.TenantSequence > 0 &&
        OperationsNotificationsDataRightsValidation.IsSha256(
            request.LedgerEntrySha256) &&
        OperationsNotificationsDataRightsValidation.IsGuestPropertyScope(
            scopeContext,
            request.TenantId,
            DataRightsCaseType.GuestRights,
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
        request.ResultingRecordVersion is > 1 &&
        request.OriginallyCompletedAtUtc != default;
}
