namespace BunkFy.Modules.Workspaces.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;

internal sealed class
    WorkspaceStaffCorrelationAnonymisationPolicyContributor(
        IWorkspaceStaffCorrelationAnonymisationRepository correlations)
    : IDataRightsAnonymisationPolicyContributor
{
    private const string InvalidRequest =
        "workspaces.staff-correlation-policy.invalid-request";
    private const string StateUnavailable =
        "workspaces.staff-correlation-policy.state-unavailable";

    public int ContractVersion =>
        DataRightsAnonymisationPolicyContract.CurrentVersion;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public string OwnerKey =>
        WorkspacesDataRightsCoordinates.Owner;

    public string RecordType =>
        WorkspacesDataRightsCoordinates
            .StaffAccessProcessRecordType;

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

        WorkspaceStaffCorrelationAnonymisationSnapshot snapshot =
            await correlations.ReadAsync(
                request.TenantId,
                request.Coordinate.RecordId,
                request.Coordinate.RecordVersion,
                cancellationToken).ConfigureAwait(false);
        if (snapshot is not
            {
                Status:
                    WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                        .Eligible,
                AnchorProcessId: not null,
                AnchorProcessVersion: not null,
                StaffMemberId: not null,
                SelectedStaffVersion: not null,
                StateSha256: not null
            } ||
            snapshot.AnchorProcessId.Value !=
                request.Coordinate.RecordId ||
            snapshot.AnchorProcessVersion.Value !=
                request.Coordinate.RecordVersion ||
            !IsSha256(snapshot.StateSha256))
        {
            return DataRightsAnonymisationPolicyContributionResult
                .Denied(StateUnavailable);
        }

        return DataRightsAnonymisationPolicyContributionResult
            .ApprovedCompanion(
                new(
                    StaffDataRightsCoordinates.Owner,
                    StaffDataRightsCoordinates.StaffMemberRecordType,
                    snapshot.StaffMemberId.Value,
                    snapshot.SelectedStaffVersion.Value),
                [
                    new(
                        WorkspacesDataRightsCoordinates
                            .StaffCorrelationStateBindingKey,
                        snapshot.AnchorProcessVersion.Value,
                        snapshot.StateSha256)
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
        string.Equals(
            request.Coordinate.OwnerKey,
            this.OwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            this.RecordType,
            StringComparison.Ordinal) &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0;

    private static bool IsSha256(string value) =>
        value.Length ==
            DataRightsAnonymisationPolicyContract.Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));
}
