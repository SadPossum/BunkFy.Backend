namespace BunkFy.Modules.Workspaces.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class
    WorkspaceStaffCorrelationDataRightsAnonymisationRestoreContributor(
        IRequestDispatcher dispatcher)
    : IDataRightsAnonymisationRestoreContributorV3
{
    public string OwnerKey =>
        WorkspacesDataRightsCoordinates.Owner;

    public string RecordType =>
        WorkspacesDataRightsCoordinates
            .StaffAccessProcessRecordType;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public int ContractVersion =>
        DataRightsAnonymisationRestoreContractV3.CurrentVersion;

    public async Task<DataRightsAnonymisationRestoreResult>
        RestoreAsync(
            DataRightsAnonymisationRestoreRequestV3 request,
            CancellationToken cancellationToken)
    {
        Result<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
            restored = await dispatcher.SendAsync(
                new RestoreWorkspaceStaffCorrelationAnonymisationCommand(
                    request),
                cancellationToken).ConfigureAwait(false);
        if (restored.IsFailure)
        {
            return DataRightsAnonymisationRestoreResult.Failed(
                this.ContractVersion,
                restored.Error.Code);
        }

        WorkspaceStaffCorrelationAnonymisationRestoreReceipt
            receipt = restored.Value;
        return DataRightsAnonymisationRestoreResult.Completed(
            this.ContractVersion,
            new(
                receipt.LedgerEntryId,
                receipt.OwnerReceiptId,
                receipt.OwnerReceiptSha256,
                receipt.ResultingAnchorVersion,
                receipt.TombstoneRevision,
                receipt.ReplayedAtUtc));
    }
}
