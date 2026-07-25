namespace BunkFy.Modules.Guests.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using Gma.Framework.Cqrs;

internal sealed class GuestDataRightsAnonymisationRestoreContributor(
    IRequestDispatcher dispatcher)
    : IDataRightsAnonymisationRestoreContributor
{
    public string OwnerKey => GuestsDataRightsCoordinates.Owner;

    public string RecordType =>
        GuestsDataRightsCoordinates.GuestProfileRecordType;

    public int ContractVersion =>
        DataRightsAnonymisationRestoreContract.CurrentVersion;

    public async Task<DataRightsAnonymisationRestoreResult> RestoreAsync(
        DataRightsAnonymisationRestoreRequest request,
        CancellationToken cancellationToken)
    {
        Gma.Framework.Results.Result<
            GuestAnonymisationRestoreReceipt> restored =
                await dispatcher.SendAsync(
                    new RestoreGuestAnonymisationCommand(request),
                    cancellationToken).ConfigureAwait(false);
        if (restored.IsFailure)
        {
            return DataRightsAnonymisationRestoreResult.Failed(
                restored.Error.Code);
        }

        GuestAnonymisationRestoreReceipt receipt = restored.Value;
        return DataRightsAnonymisationRestoreResult.Completed(
            new(
                receipt.LedgerEntryId,
                receipt.OwnerReceiptId,
                receipt.OwnerReceiptSha256,
                receipt.ResultingGuestVersion,
                receipt.TombstoneRevision,
                receipt.ReplayedAtUtc));
    }
}
