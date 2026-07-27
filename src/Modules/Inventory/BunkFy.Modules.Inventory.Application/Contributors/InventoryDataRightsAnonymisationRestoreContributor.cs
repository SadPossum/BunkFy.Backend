namespace BunkFy.Modules.Inventory.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class
    InventoryDataRightsAnonymisationRestoreContributor(
        IRequestDispatcher dispatcher)
    : IDataRightsAnonymisationRestoreContributor
{
    public string OwnerKey => InventoryDataRightsCoordinates.Owner;

    public string RecordType =>
        InventoryDataRightsCoordinates.AllocationRecordType;

    public int ContractVersion =>
        DataRightsAnonymisationRestoreContract.CurrentVersion;

    public async Task<DataRightsAnonymisationRestoreResult>
        RestoreAsync(
            DataRightsAnonymisationRestoreRequest request,
            CancellationToken cancellationToken)
    {
        Result<InventoryAllocationAnonymisationRestoreReceipt>
            restored = await dispatcher.SendAsync(
                new RestoreInventoryAllocationAnonymisationCommand(
                    request),
                cancellationToken).ConfigureAwait(false);
        if (restored.IsFailure)
        {
            return DataRightsAnonymisationRestoreResult.Failed(
                restored.Error.Code);
        }

        InventoryAllocationAnonymisationRestoreReceipt receipt =
            restored.Value;
        return DataRightsAnonymisationRestoreResult.Completed(
            new(
                receipt.LedgerEntryId,
                receipt.OwnerReceiptId,
                receipt.OwnerReceiptSha256,
                receipt.ResultingAllocationVersion,
                receipt.TombstoneRevision,
                receipt.ReplayedAtUtc));
    }
}
