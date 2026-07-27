namespace BunkFy.Modules.Guests.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Contracts;

internal sealed class GuestDataRightsCorrectionPolicyContributor
    : IDataRightsCorrectionPolicyContributor
{
    public int ContractVersion => DataRightsCorrectionContract.CurrentVersion;
    public string OwnerKey => GuestsDataRightsCoordinates.Owner;
    public string RecordType => GuestsDataRightsCoordinates.GuestProfileRecordType;
    public string FieldPolicyKey => GuestsDataRightsCoordinates.CorrectionFieldPolicyKey;
}
