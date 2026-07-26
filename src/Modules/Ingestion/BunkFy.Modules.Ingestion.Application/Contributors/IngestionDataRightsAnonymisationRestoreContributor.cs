namespace BunkFy.Modules.Ingestion.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class IngestionDataRightsAnonymisationRestoreContributor(
    IRequestDispatcher dispatcher,
    IRawPayloadStore rawPayloads)
    : IDataRightsAnonymisationRestoreContributor
{
    public string OwnerKey => IngestionDataRightsCoordinates.Owner;

    public string RecordType =>
        IngestionDataRightsCoordinates.ReservationSourceLinkRecordType;

    public int ContractVersion =>
        DataRightsAnonymisationRestoreContract.CurrentVersion;

    public async Task<DataRightsAnonymisationRestoreResult> RestoreAsync(
        DataRightsAnonymisationRestoreRequest request,
        CancellationToken cancellationToken)
    {
        Result<IngestionAnonymisationRestoreStage> started =
            await dispatcher.SendAsync(
                new BeginIngestionAnonymisationRestoreCommand(request),
                cancellationToken).ConfigureAwait(false);
        if (started.IsFailure)
        {
            return DataRightsAnonymisationRestoreResult.Failed(
                started.Error.Code);
        }

        foreach (IngestionRawPayloadDeletion deletion in
                 started.Value.RawPayloads)
        {
            _ = await rawPayloads.DeleteAsync(
                deletion.FileId,
                request.TenantId,
                deletion.ConnectionId,
                cancellationToken).ConfigureAwait(false);
            RawPayloadRead? raw = await rawPayloads.ReadAsync(
                deletion.FileId,
                request.TenantId,
                deletion.ConnectionId,
                cancellationToken).ConfigureAwait(false);
            if (raw is not null)
            {
                return DataRightsAnonymisationRestoreResult.Failed(
                    IngestionApplicationErrors
                        .AnonymisationRawPayloadDeletionIncomplete.Code);
            }
        }

        Result<DataRightsAnonymisationRestoreProof> completed =
            await dispatcher.SendAsync(
                new CompleteIngestionAnonymisationRestoreCommand(request),
                cancellationToken).ConfigureAwait(false);
        return completed.IsFailure
            ? DataRightsAnonymisationRestoreResult.Failed(
                completed.Error.Code)
            : DataRightsAnonymisationRestoreResult.Completed(
                completed.Value);
    }
}
