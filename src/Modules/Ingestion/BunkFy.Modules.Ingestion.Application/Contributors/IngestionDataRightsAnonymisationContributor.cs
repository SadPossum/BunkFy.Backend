namespace BunkFy.Modules.Ingestion.Application.Contributors;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class IngestionDataRightsAnonymisationContributor(
    IRequestDispatcher dispatcher,
    IRawPayloadStore rawPayloads)
    : IDataRightsAnonymisationContributor
{
    private const string BlockedPrefix =
        "Ingestion.AnonymisationBlocked.";
    private const string CompletedDispositionCode =
        "ingestion.completed";
    private const string ProviderEvidenceAnonymisedReasonCode =
        "ingestion.provider-evidence-anonymised";

    public string OwnerKey =>
        IngestionDataRightsCoordinates.Owner;

    public int ContractVersion =>
        DataRightsAnonymisationContract.CurrentVersion;

    public async Task<
        DataRightsAnonymisationContributionResult> ExecuteAsync(
            DataRightsAnonymisationContributionRequest request,
            CancellationToken cancellationToken)
    {
        Result<IngestionAnonymisationExecutionStage> started =
            await dispatcher.SendAsync(
                new BeginIngestionAnonymisationCommand(request),
                cancellationToken).ConfigureAwait(false);
        if (started.IsFailure)
        {
            return FromError(started.Error);
        }

        if (started.Value.CompletedProof is { } existing)
        {
            return Completed(existing);
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
                return DataRightsAnonymisationContributionResult
                    .Failed(
                        IngestionApplicationErrors
                            .AnonymisationRawPayloadDeletionIncomplete
                            .Code);
            }
        }

        Result<IngestionAnonymisationExecutionProof> completed =
            await dispatcher.SendAsync(
                new CompleteIngestionAnonymisationCommand(request),
                cancellationToken).ConfigureAwait(false);
        return completed.IsFailure
            ? FromError(completed.Error)
            : Completed(completed.Value);
    }

    private static DataRightsAnonymisationContributionResult
        Completed(IngestionAnonymisationExecutionProof proof) =>
        DataRightsAnonymisationContributionResult.Completed(
            new DataRightsAnonymisationOwnerProof(
                proof.ReceiptContractVersion,
                proof.ReceiptId,
                proof.ResultingSourceLinkVersion,
                CompletedDispositionCode,
                ProviderEvidenceAnonymisedReasonCode,
                proof.ReceiptSha256,
                proof.CompletedAtUtc));

    private static DataRightsAnonymisationContributionResult
        FromError(Error error) =>
        error.Code.StartsWith(
            BlockedPrefix,
            StringComparison.Ordinal)
            ? DataRightsAnonymisationContributionResult.Blocked(
                error.Code)
            : DataRightsAnonymisationContributionResult.Failed(
                error.Code);
}
