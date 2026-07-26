namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionAnonymisationRestoreRepository(
    IngestionDbContext dbContext,
    IngestionDataRightsEvidenceGraphLoader graphLoader)
    : IIngestionAnonymisationRestoreRepository
{
    public Task<IngestionAnonymisationTombstone?> GetTombstoneAsync(
        Guid sourceLinkId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationTombstones.SingleOrDefaultAsync(
            tombstone => tombstone.Id == sourceLinkId,
            cancellationToken);

    public async Task<
        IReadOnlyList<IngestionAnonymisationRecordPlanEntry>> GetPlanAsync(
            Guid tombstoneId,
            CancellationToken cancellationToken) =>
        await dbContext.AnonymisationRecordPlan
            .AsNoTracking()
            .Where(entry => entry.TombstoneId == tombstoneId)
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.RecordId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountFingerprintsAsync(
        Guid tombstoneId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationFingerprints
            .AsNoTracking()
            .CountAsync(
                fingerprint => fingerprint.TombstoneId == tombstoneId,
                cancellationToken);

    public async Task<IngestionAnonymisationRestoreGraphLoadResult>
        LoadInitialGraphAsync(
            Guid propertyId,
            Guid sourceLinkId,
            CancellationToken cancellationToken)
    {
        ReservationSourceLink? sourceLink =
            await dbContext.ReservationSourceLinks
                .SingleOrDefaultAsync(
                    link =>
                        link.PropertyId == propertyId &&
                        link.Id == sourceLinkId &&
                        link.ReservationId != null,
                    cancellationToken)
                .ConfigureAwait(false);
        if (sourceLink is null)
        {
            return IngestionAnonymisationRestoreGraphLoadResult.NotFound();
        }

        IngestionDataRightsEvidenceGraphLoadResult loaded =
            await graphLoader.LoadTrackedAsync(
                sourceLink,
                cancellationToken).ConfigureAwait(false);
        return loaded.Status switch
        {
            IngestionDataRightsEvidenceGraphLoadStatus.Succeeded
                when loaded.Graph is { } graph =>
                IngestionAnonymisationRestoreGraphLoadResult.Found(
                    ToRestoreGraph(sourceLink, graph)),
            IngestionDataRightsEvidenceGraphLoadStatus.TooLarge =>
                IngestionAnonymisationRestoreGraphLoadResult.TooLarge(),
            _ =>
                IngestionAnonymisationRestoreGraphLoadResult.Unavailable()
        };
    }

    public async Task<IngestionAnonymisationRestoreGraphLoadResult>
        LoadPlannedGraphAsync(
            Guid propertyId,
            Guid sourceLinkId,
            IReadOnlyCollection<IngestionAnonymisationRecordPlanEntry> plan,
            CancellationToken cancellationToken)
    {
        if (plan is null || plan.Count == 0)
        {
            return IngestionAnonymisationRestoreGraphLoadResult.Unavailable();
        }

        IngestionAnonymisationRecordPlanEntry[] sourceEntries = plan
            .Where(entry =>
                entry.Kind ==
                    IngestionAnonymisationRecordKind.ReservationSourceLink)
            .ToArray();
        if (sourceEntries.Length != 1 ||
            sourceEntries[0].RecordId != sourceLinkId)
        {
            return IngestionAnonymisationRestoreGraphLoadResult.Unavailable();
        }

        ReservationSourceLink? sourceLink =
            await dbContext.ReservationSourceLinks.SingleOrDefaultAsync(
                link =>
                    link.PropertyId == propertyId &&
                    link.Id == sourceLinkId,
                cancellationToken).ConfigureAwait(false);
        if (sourceLink is null)
        {
            return IngestionAnonymisationRestoreGraphLoadResult.NotFound();
        }

        Guid[] proposalIds = IdsFor(
            plan,
            IngestionAnonymisationRecordKind.ChangeProposal);
        Guid[] dispatchIds = IdsFor(
            plan,
            IngestionAnonymisationRecordKind.ReservationDispatch);
        Guid[] receiptIds = IdsFor(
            plan,
            IngestionAnonymisationRecordKind.ObservationReceipt);
        Guid[] attemptIds = IdsFor(
            plan,
            IngestionAnonymisationRecordKind
                .ObservationReprocessingAttempt);
        Guid[] outputIds = IdsFor(
            plan,
            IngestionAnonymisationRecordKind
                .ObservationReprocessingOutput);

        ChangeProposal[] proposals = await dbContext.ChangeProposals
            .Where(item =>
                item.PropertyId == propertyId &&
                proposalIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        ReservationDispatch[] dispatches =
            await dbContext.ReservationDispatches
                .Where(item =>
                    item.PropertyId == propertyId &&
                    dispatchIds.Contains(item.Id))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        ObservationReceipt[] receipts =
            await dbContext.ObservationReceipts
                .Where(item =>
                    item.PropertyId == propertyId &&
                    receiptIds.Contains(item.Id))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        ObservationReprocessingAttempt[] attempts =
            await dbContext.ObservationReprocessingAttempts
                .Where(item =>
                    item.PropertyId == propertyId &&
                    attemptIds.Contains(item.Id))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        ObservationReprocessingOutput[] outputs =
            await dbContext.ObservationReprocessingOutputs
                .Where(item => outputIds.Contains(item.Id))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

        if (proposals.Length != proposalIds.Length ||
            dispatches.Length != dispatchIds.Length ||
            receipts.Length != receiptIds.Length ||
            attempts.Length != attemptIds.Length ||
            outputs.Length != outputIds.Length)
        {
            return IngestionAnonymisationRestoreGraphLoadResult.Unavailable();
        }

        IngestionAnonymisationRestoreGraph graph = new(
            sourceLink,
            proposals,
            dispatches,
            receipts,
            attempts,
            outputs);
        return graph.RecordCount == plan.Count
            ? IngestionAnonymisationRestoreGraphLoadResult.Found(graph)
            : IngestionAnonymisationRestoreGraphLoadResult.Unavailable();
    }

    public void AddRestoreState(
        IngestionAnonymisationTombstone tombstone,
        IReadOnlyCollection<IngestionAnonymisationFingerprint> fingerprints,
        IReadOnlyCollection<IngestionAnonymisationRecordPlanEntry> plan)
    {
        dbContext.AnonymisationTombstones.Add(tombstone);
        dbContext.AnonymisationFingerprints.AddRange(fingerprints);
        dbContext.AnonymisationRecordPlan.AddRange(plan);
    }

    private static IngestionAnonymisationRestoreGraph ToRestoreGraph(
        ReservationSourceLink sourceLink,
        IngestionDataRightsEvidenceGraph graph) =>
        new(
            sourceLink,
            graph.Proposals,
            graph.Dispatches,
            graph.Receipts,
            graph.Attempts,
            graph.Outputs);

    private static Guid[] IdsFor(
        IReadOnlyCollection<IngestionAnonymisationRecordPlanEntry> plan,
        IngestionAnonymisationRecordKind kind) =>
        plan.Where(entry => entry.Kind == kind)
            .Select(entry => entry.RecordId)
            .Distinct()
            .ToArray();
}
