namespace BunkFy.Modules.Reservations.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class ReservationDataRightsAnonymisationRestoreContributor(
    IRequestDispatcher dispatcher)
    : IDataRightsAnonymisationRestoreContributor
{
    public string OwnerKey => ReservationsDataRightsCoordinates.Owner;

    public string RecordType =>
        ReservationsDataRightsCoordinates.ReservationRecordType;

    public int ContractVersion =>
        DataRightsAnonymisationRestoreContract.CurrentVersion;

    public async Task<DataRightsAnonymisationRestoreResult> RestoreAsync(
        DataRightsAnonymisationRestoreRequest request,
        CancellationToken cancellationToken)
    {
        Result<ReservationAnonymisationRestoreReceipt> restored =
            await dispatcher.SendAsync(
                new RestoreReservationAnonymisationCommand(request),
                cancellationToken).ConfigureAwait(false);
        if (restored.IsFailure)
        {
            return DataRightsAnonymisationRestoreResult.Failed(
                restored.Error.Code);
        }

        ReservationAnonymisationRestoreReceipt receipt =
            restored.Value;
        return DataRightsAnonymisationRestoreResult.Completed(
            new(
                receipt.LedgerEntryId,
                receipt.OwnerReceiptId,
                receipt.OwnerReceiptSha256,
                receipt.ResultingReservationVersion,
                receipt.TombstoneRevision,
                receipt.ReplayedAtUtc));
    }
}
