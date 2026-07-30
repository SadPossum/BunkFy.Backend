namespace BunkFy.Modules.Workspaces.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;

internal sealed class
    WorkspaceStaffOnboardingDataRightsCorrectionPolicyContributor
    : IDataRightsCorrectionPolicyContributor
{
    public int ContractVersion =>
        DataRightsCorrectionContract.CurrentVersion;

    public string OwnerKey => WorkspacesDataRightsCoordinates.Owner;

    public string RecordType =>
        WorkspacesDataRightsCoordinates.StaffOnboardingRecordType;

    public string FieldPolicyKey =>
        WorkspacesDataRightsCoordinates
            .StaffOnboardingCorrectionFieldPolicyKey;
}
