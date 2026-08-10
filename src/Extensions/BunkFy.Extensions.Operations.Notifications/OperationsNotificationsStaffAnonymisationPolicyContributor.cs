namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Modules.Notifications.Contracts;

internal sealed class
    OperationsNotificationsStaffAnonymisationPolicyContributor(
        INotificationHistoryLifecycle lifecycle,
        IStaffDataRightsAuthorityReader staffAuthority)
    : IDataRightsAnonymisationPolicyContributor
{
    private const string InvalidRequest =
        "operations-notifications.staff-policy.invalid-request";
    private const string AuthorityUnavailable =
        "operations-notifications.staff-policy.authority-unavailable";
    private const string HistoryUnavailable =
        "operations-notifications.staff-policy.history-unavailable";

    public int ContractVersion =>
        DataRightsAnonymisationPolicyContract.CurrentVersion;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public string OwnerKey =>
        OperationsNotificationsDataRightsCoordinates.Owner;

    public string RecordType =>
        OperationsNotificationsDataRightsCoordinates
            .StaffInboxHistoryRecordType;

    public async Task<DataRightsAnonymisationPolicyContributionResult>
        EvaluateAsync(
            DataRightsAnonymisationPolicyContributionRequest request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValid(request))
        {
            return DataRightsAnonymisationPolicyContributionResult
                .Denied(InvalidRequest);
        }

        StaffDataRightsAuthorityState? authority =
            await staffAuthority.ReadAsync(
                    request.TenantId,
                    request.Coordinate.RecordId,
                    cancellationToken)
                .ConfigureAwait(false);
        if (authority is null ||
            authority.StaffMemberId != request.Coordinate.RecordId ||
            authority.Version <= 0 ||
            authority.State !=
                StaffDataRightsAuthorityRecordState.Departed)
        {
            return DataRightsAnonymisationPolicyContributionResult
                .Denied(AuthorityUnavailable);
        }

        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates.ForStaff(
                request.TenantId,
                request.Coordinate.RecordId);
        NotificationHistoryReferenceSnapshot snapshot =
            await lifecycle.GetSnapshotAsync(
                    request.TenantId,
                    reference,
                    cancellationToken)
                .ConfigureAwait(false);
        if (snapshot.Status !=
                NotificationHistoryReferenceStatus.Open ||
            snapshot.Version != request.Coordinate.RecordVersion)
        {
            return DataRightsAnonymisationPolicyContributionResult
                .Denied(HistoryUnavailable);
        }

        return DataRightsAnonymisationPolicyContributionResult
            .ApprovedCompanion(
                new DataRightsSubjectCoordinate(
                    StaffDataRightsCoordinates.Owner,
                    StaffDataRightsCoordinates.StaffMemberRecordType,
                    authority.StaffMemberId,
                    authority.Version),
                [
                    OperationsNotificationsStaffHistoryPolicyEvidence
                        .CreateBinding(reference, snapshot)
                ]);
    }

    private bool IsValid(
        DataRightsAnonymisationPolicyContributionRequest? request) =>
        request is not null &&
        request.ContractVersion == this.ContractVersion &&
        request.CaseType == this.CaseType &&
        request.PropertyId is null &&
        request.CaseId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        OperationsNotificationsDataRightsValidation
            .IsStaffHistoryCoordinate(request.Coordinate);
}
