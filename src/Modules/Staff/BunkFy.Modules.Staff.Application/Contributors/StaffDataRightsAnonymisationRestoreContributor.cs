namespace BunkFy.Modules.Staff.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class StaffDataRightsAnonymisationRestoreContributor(
    IRequestDispatcher dispatcher)
    : IDataRightsAnonymisationRestoreContributorV3
{
    public string OwnerKey => StaffDataRightsCoordinates.Owner;

    public string RecordType =>
        StaffDataRightsCoordinates.StaffMemberRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public int ContractVersion =>
        DataRightsAnonymisationRestoreContractV3.CurrentVersion;

    public async Task<DataRightsAnonymisationRestoreResult> RestoreAsync(
        DataRightsAnonymisationRestoreRequestV3 request,
        CancellationToken cancellationToken)
    {
        Result<StaffAnonymisationRestoreReceipt> restored =
            await dispatcher.SendAsync(
                new RestoreStaffAnonymisationCommand(request),
                cancellationToken).ConfigureAwait(false);
        if (restored.IsFailure)
        {
            return DataRightsAnonymisationRestoreResult.Failed(
                this.ContractVersion,
                restored.Error.Code);
        }

        StaffAnonymisationRestoreReceipt receipt = restored.Value;
        return DataRightsAnonymisationRestoreResult.Completed(
            this.ContractVersion,
            new(
                receipt.LedgerEntryId,
                receipt.OwnerReceiptId,
                receipt.OwnerReceiptSha256,
                receipt.ResultingStaffVersion,
                receipt.TombstoneRevision,
                receipt.ReplayedAtUtc));
    }
}
