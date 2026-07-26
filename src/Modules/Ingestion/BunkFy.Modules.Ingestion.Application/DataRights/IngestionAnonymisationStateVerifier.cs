namespace BunkFy.Modules.Ingestion.Application.DataRights;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reservations;

internal static class IngestionAnonymisationStateVerifier
{
    public static bool Matches(
        IngestionAnonymisationRestoreGraph graph,
        IReadOnlyCollection<IngestionAnonymisationRecordPlanEntry> plan,
        Guid claimId,
        bool completed)
    {
        if (graph.RecordCount != plan.Count ||
            plan.Select(entry => new { entry.Kind, entry.RecordId })
                .Distinct()
                .Count() != plan.Count)
        {
            return false;
        }

        Dictionary<
            (IngestionAnonymisationRecordKind Kind, Guid RecordId),
            IngestionAnonymisationRecordPlanEntry> entries =
            plan.ToDictionary(entry => (entry.Kind, entry.RecordId));

        return MatchesSourceLink(graph.SourceLink, entries, completed) &&
            graph.Receipts.All(receipt =>
                MatchesReceipt(receipt, entries, claimId, completed)) &&
            graph.Proposals.All(proposal =>
                MatchesEntry(
                    entries,
                    IngestionAnonymisationRecordKind.ChangeProposal,
                    proposal.Id,
                    proposal.Version,
                    completed) &&
                proposal.AnonymisedAtUtc.HasValue &&
                proposal.ReservationId == Guid.Empty &&
                proposal.Diff is null &&
                proposal.DecisionReason is null) &&
            graph.Dispatches.All(dispatch =>
                MatchesEntry(
                    entries,
                    IngestionAnonymisationRecordKind.ReservationDispatch,
                    dispatch.Id,
                    dispatch.Version,
                    completed) &&
                dispatch.AnonymisedAtUtc.HasValue &&
                dispatch.ReservationId is null &&
                dispatch.SourceRevision is null &&
                dispatch.NormalizedSnapshot is null) &&
            graph.Attempts.All(attempt =>
                MatchesEntry(
                    entries,
                    IngestionAnonymisationRecordKind
                        .ObservationReprocessingAttempt,
                    attempt.Id,
                    attempt.Version,
                    completed)) &&
            graph.Outputs.All(output =>
                MatchesEntry(
                    entries,
                    IngestionAnonymisationRecordKind
                        .ObservationReprocessingOutput,
                    output.Id,
                    output.Version,
                    completed) &&
                output.AnonymisedAtUtc.HasValue &&
                output.ExternalId == $"anonymised:{output.Id:N}" &&
                output.SourceRevision is null &&
                output.ContentHash.All(character => character == '0'));
    }

    private static bool MatchesSourceLink(
        ReservationSourceLink sourceLink,
        Dictionary<
            (IngestionAnonymisationRecordKind Kind, Guid RecordId),
            IngestionAnonymisationRecordPlanEntry> entries,
        bool completed) =>
        MatchesEntry(
            entries,
            IngestionAnonymisationRecordKind.ReservationSourceLink,
            sourceLink.Id,
            sourceLink.Version,
            completed) &&
        sourceLink.State == ReservationSourceLinkState.Anonymised &&
        sourceLink.AnonymisedAtUtc.HasValue &&
        sourceLink.SourceReference == $"anonymised:{sourceLink.Id:N}" &&
        sourceLink.ReservationId is null &&
        sourceLink.LastObservedSourceRevision is null &&
        sourceLink.LastAppliedSourceRevision is null &&
        sourceLink.LastAppliedOperationalBaseline is null &&
        sourceLink.LastObservedContentHash.All(
            character => character == '0');

    private static bool MatchesReceipt(
        ObservationReceipt receipt,
        Dictionary<
            (IngestionAnonymisationRecordKind Kind, Guid RecordId),
            IngestionAnonymisationRecordPlanEntry> entries,
        Guid claimId,
        bool completed)
    {
        if (!entries.TryGetValue(
                (
                    IngestionAnonymisationRecordKind.ObservationReceipt,
                    receipt.Id),
                out IngestionAnonymisationRecordPlanEntry? entry) ||
            !MatchesVersion(entry, receipt.RawPayloadVersion, completed) ||
            !receipt.AnonymisedAtUtc.HasValue ||
            receipt.ExternalId != $"anonymised:{receipt.Id:N}" ||
            receipt.DeduplicationKey != $"anonymised:{receipt.Id:N}" ||
            receipt.SourceRevision is not null ||
            receipt.RejectionReason is not null ||
            !receipt.ContentHash.All(character => character == '0'))
        {
            return false;
        }

        if (!entry.RequiresRawPayloadDeletion)
        {
            return receipt.RawPayloadRetentionState ==
                RawPayloadRetentionState.Purged;
        }

        return completed
            ? receipt.RawPayloadRetentionState ==
                RawPayloadRetentionState.Purged &&
              receipt.RawPayloadPurgeClaimId is null
            : receipt.RawPayloadRetentionState ==
                RawPayloadRetentionState.Purging &&
              receipt.RawPayloadPurgeClaimId == claimId;
    }

    private static bool MatchesEntry(
        Dictionary<
            (IngestionAnonymisationRecordKind Kind, Guid RecordId),
            IngestionAnonymisationRecordPlanEntry> entries,
        IngestionAnonymisationRecordKind kind,
        Guid recordId,
        long version,
        bool completed) =>
        entries.TryGetValue(
            (kind, recordId),
            out IngestionAnonymisationRecordPlanEntry? entry) &&
        MatchesVersion(entry, version, completed);

    private static bool MatchesVersion(
        IngestionAnonymisationRecordPlanEntry entry,
        long version,
        bool completed) =>
        completed
            ? entry.MatchesVersion(version)
            : entry.MatchesReductionVersion(version);
}
