namespace BunkFy.Modules.Reservations.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class ReservationDataRightsCorrectionPolicyContributor
    : IDataRightsCorrectionPolicyContributor
{
    public int ContractVersion => DataRightsCorrectionContract.CurrentVersion;
    public string OwnerKey => ReservationsDataRightsCoordinates.Owner;
    public string RecordType => ReservationsDataRightsCoordinates.ReservationRecordType;
    public string FieldPolicyKey =>
        ReservationsDataRightsCoordinates.CorrectionFieldPolicyKey;
}
