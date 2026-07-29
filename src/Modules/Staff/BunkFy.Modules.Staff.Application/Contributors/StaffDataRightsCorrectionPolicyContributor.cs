namespace BunkFy.Modules.Staff.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;

internal sealed class StaffDataRightsCorrectionPolicyContributor
    : IDataRightsCorrectionPolicyContributor
{
    public int ContractVersion => DataRightsCorrectionContract.CurrentVersion;
    public string OwnerKey => StaffDataRightsCoordinates.Owner;
    public string RecordType => StaffDataRightsCoordinates.StaffMemberRecordType;
    public string FieldPolicyKey =>
        StaffDataRightsCoordinates.CorrectionFieldPolicyKey;
}
