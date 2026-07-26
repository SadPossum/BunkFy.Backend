namespace BunkFy.Modules.Ingestion.Application.DataRights;

using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Domain.DataRights;

internal static class IngestionAnonymisationExecutionStageFactory
{
    public static IngestionAnonymisationExecutionStage Create(
        IngestionAnonymisationTombstone tombstone,
        IEnumerable<IngestionAnonymisationRecordPlanEntry> plan,
        IngestionAnonymisationReceipt? receipt = null) =>
        new(
            tombstone.Id,
            plan.Where(entry =>
                    entry.RequiresRawPayloadDeletion)
                .OrderBy(entry =>
                    entry.RawPayloadConnectionId)
                .ThenBy(entry => entry.RawPayloadFileId)
                .Select(entry =>
                    new IngestionRawPayloadDeletion(
                        entry.RawPayloadFileId!.Value,
                        entry.RawPayloadConnectionId!.Value))
                .ToArray(),
            receipt is null ? null : CreateProof(receipt));

    public static IngestionAnonymisationExecutionProof CreateProof(
        IngestionAnonymisationReceipt receipt) =>
        new(
            receipt.ContractVersion,
            receipt.Id,
            receipt.ResultingSourceLinkVersion,
            receipt.CanonicalSha256,
            receipt.CompletedAtUtc);

    public static bool MatchesCompletedProof(
        IngestionAnonymisationTombstone tombstone,
        IngestionAnonymisationReceipt receipt) =>
        tombstone.State ==
            IngestionAnonymisationTombstoneState.Completed &&
        tombstone.MatchesOwnerProof(
            tombstone.PropertyId,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingSourceLinkVersion,
            receipt.CompletedAtUtc) &&
        receipt.GraphRecordCount == tombstone.GraphRecordCount &&
        receipt.FingerprintCount == tombstone.FingerprintCount &&
        receipt.RawPayloadCount == tombstone.RawPayloadCount;
}
