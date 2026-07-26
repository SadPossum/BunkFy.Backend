namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionDataRightsEvidenceGraphLoader(
    IngestionDbContext dbContext)
{
    public const int MaximumGraphRecords = 1_000;

    public async Task<IngestionDataRightsEvidenceGraphLoadResult> LoadAsync(
        ReservationSourceLink sourceLink,
        CancellationToken cancellationToken) =>
        await this.LoadCoreAsync(
            sourceLink,
            trackChanges: false,
            cancellationToken).ConfigureAwait(false);

    public async Task<IngestionDataRightsEvidenceGraphLoadResult>
        LoadTrackedAsync(
            ReservationSourceLink sourceLink,
            CancellationToken cancellationToken) =>
        await this.LoadCoreAsync(
            sourceLink,
            trackChanges: true,
            cancellationToken).ConfigureAwait(false);

    private async Task<IngestionDataRightsEvidenceGraphLoadResult>
        LoadCoreAsync(
            ReservationSourceLink sourceLink,
            bool trackChanges,
            CancellationToken cancellationToken)
    {
        ReservationDispatch[] dispatches = await Query(
                dbContext.ReservationDispatches,
                trackChanges)
            .Where(dispatch =>
                dispatch.PropertyId == sourceLink.PropertyId &&
                dispatch.SourceLinkId == sourceLink.Id)
            .OrderBy(dispatch => dispatch.CreatedAtUtc)
            .ThenBy(dispatch => dispatch.Id)
            .Take(MaximumGraphRecords + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (dispatches.Length > MaximumGraphRecords)
        {
            return IngestionDataRightsEvidenceGraphLoadResult.TooLarge();
        }

        ChangeProposal[] proposals = await Query(
                dbContext.ChangeProposals,
                trackChanges)
            .Where(proposal =>
                proposal.PropertyId == sourceLink.PropertyId &&
                proposal.ConnectionId == sourceLink.ConnectionId &&
                proposal.ReservationId == sourceLink.ReservationId)
            .OrderBy(proposal => proposal.CreatedAtUtc)
            .ThenBy(proposal => proposal.Id)
            .Take(MaximumGraphRecords - dispatches.Length + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (dispatches.Length + proposals.Length > MaximumGraphRecords)
        {
            return IngestionDataRightsEvidenceGraphLoadResult.TooLarge();
        }

        Dictionary<Guid, ObservationReceipt> receipts =
            (await Query(
                    dbContext.ObservationReceipts,
                    trackChanges)
                .Where(receipt =>
                    receipt.PropertyId == sourceLink.PropertyId &&
                    receipt.ConnectionId == sourceLink.ConnectionId &&
                    receipt.ExternalId == sourceLink.SourceReference)
                .OrderBy(receipt => receipt.ReceivedAtUtc)
                .ThenBy(receipt => receipt.Id)
                .Take(
                    MaximumGraphRecords -
                    dispatches.Length -
                    proposals.Length +
                    1)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(receipt => receipt.Id);
        if (GraphRecordCount(dispatches, proposals, receipts.Count) >
            MaximumGraphRecords)
        {
            return IngestionDataRightsEvidenceGraphLoadResult.TooLarge();
        }

        HashSet<Guid> requiredReceiptIds =
        [
            sourceLink.LastObservedReceiptId
        ];
        AddIfPresent(requiredReceiptIds, sourceLink.LastAppliedReceiptId);
        AddIfPresent(requiredReceiptIds, sourceLink.DeferredReceiptId);
        requiredReceiptIds.UnionWith(
            dispatches.Select(dispatch => dispatch.ReceiptId));
        requiredReceiptIds.UnionWith(
            proposals.Select(proposal => proposal.ReceiptId));

        IngestionDataRightsEvidenceGraphLoadStatus receiptLoadStatus =
            await this.AddReceiptsByIdAsync(
                sourceLink,
                requiredReceiptIds,
                receipts,
                dispatches.Length + proposals.Length,
                trackChanges,
                cancellationToken).ConfigureAwait(false);
        if (receiptLoadStatus !=
            IngestionDataRightsEvidenceGraphLoadStatus.Succeeded)
        {
            return new(receiptLoadStatus, Graph: null);
        }

        while (true)
        {
            Guid[] parentIds = receipts.Keys.ToArray();
            int remaining = MaximumGraphRecords -
                GraphRecordCount(dispatches, proposals, receipts.Count);
            ObservationReceipt[] descendants =
                await Query(
                        dbContext.ObservationReceipts,
                        trackChanges)
                    .Where(receipt =>
                        receipt.PropertyId == sourceLink.PropertyId &&
                        receipt.ConnectionId == sourceLink.ConnectionId &&
                        receipt.SourceReceiptId != null &&
                        parentIds.Contains(receipt.SourceReceiptId.Value) &&
                        !parentIds.Contains(receipt.Id))
                    .OrderBy(receipt => receipt.ReceivedAtUtc)
                    .ThenBy(receipt => receipt.Id)
                    .Take(remaining + 1)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (descendants.Length > remaining)
            {
                return IngestionDataRightsEvidenceGraphLoadResult.TooLarge();
            }

            int added = 0;
            foreach (ObservationReceipt receipt in descendants)
            {
                if (receipts.TryAdd(receipt.Id, receipt))
                {
                    added++;
                }
            }

            if (added == 0)
            {
                break;
            }
        }

        Guid[] receiptIds = receipts.Keys.ToArray();
        int attemptCapacity = MaximumGraphRecords -
            GraphRecordCount(dispatches, proposals, receipts.Count);
        ObservationReprocessingAttempt[] attempts =
            await Query(
                    dbContext.ObservationReprocessingAttempts,
                    trackChanges)
                .Where(attempt =>
                    attempt.PropertyId == sourceLink.PropertyId &&
                    attempt.ConnectionId == sourceLink.ConnectionId &&
                    receiptIds.Contains(attempt.SourceReceiptId))
                .OrderBy(attempt => attempt.RequestedAtUtc)
                .ThenBy(attempt => attempt.Id)
                .Take(attemptCapacity + 1)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        if (attempts.Length > attemptCapacity)
        {
            return IngestionDataRightsEvidenceGraphLoadResult.TooLarge();
        }

        Guid[] attemptIds = attempts.Select(attempt => attempt.Id).ToArray();
        if (receipts.Values.Any(receipt =>
                receipt.ReprocessingAttemptId.HasValue &&
                !attemptIds.Contains(receipt.ReprocessingAttemptId.Value)))
        {
            return IngestionDataRightsEvidenceGraphLoadResult.Incomplete();
        }

        int outputCapacity = MaximumGraphRecords -
            GraphRecordCount(dispatches, proposals, receipts.Count) -
            attempts.Length;
        ObservationReprocessingOutput[] outputs = attemptIds.Length == 0
            ? []
            : await Query(
                    dbContext.ObservationReprocessingOutputs,
                    trackChanges)
                .Where(output => attemptIds.Contains(output.AttemptId))
                .OrderBy(output => output.AttemptId)
                .ThenBy(output => output.OutputIndex)
                .ThenBy(output => output.Id)
                .Take(outputCapacity + 1)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        if (outputs.Length > outputCapacity)
        {
            return IngestionDataRightsEvidenceGraphLoadResult.TooLarge();
        }

        if (outputs.Any(output =>
                output.ReceiptId.HasValue &&
                !receipts.ContainsKey(output.ReceiptId.Value)))
        {
            return IngestionDataRightsEvidenceGraphLoadResult.Incomplete();
        }

        ObservationReceipt[] orderedReceipts = receipts.Values
            .OrderBy(receipt => receipt.ReceivedAtUtc)
            .ThenBy(receipt => receipt.Id)
            .ToArray();
        return IngestionDataRightsEvidenceGraphLoadResult.Succeeded(
            new IngestionDataRightsEvidenceGraph(
                proposals,
                dispatches,
                orderedReceipts,
                attempts,
                outputs));
    }

    private async Task<IngestionDataRightsEvidenceGraphLoadStatus>
        AddReceiptsByIdAsync(
            ReservationSourceLink sourceLink,
            IReadOnlyCollection<Guid> requiredIds,
            Dictionary<Guid, ObservationReceipt> receipts,
            int existingGraphCount,
            bool trackChanges,
            CancellationToken cancellationToken)
    {
        Guid[] missingIds = requiredIds
            .Where(id => id != Guid.Empty && !receipts.ContainsKey(id))
            .Distinct()
            .ToArray();
        if (missingIds.Length == 0)
        {
            return IngestionDataRightsEvidenceGraphLoadStatus.Succeeded;
        }

        int remaining =
            MaximumGraphRecords - existingGraphCount - receipts.Count;
        if (missingIds.Length > remaining)
        {
            return IngestionDataRightsEvidenceGraphLoadStatus.TooLarge;
        }

        ObservationReceipt[] found = await Query(
                dbContext.ObservationReceipts,
                trackChanges)
            .Where(receipt =>
                receipt.PropertyId == sourceLink.PropertyId &&
                receipt.ConnectionId == sourceLink.ConnectionId &&
                missingIds.Contains(receipt.Id))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (found.Length != missingIds.Length)
        {
            return IngestionDataRightsEvidenceGraphLoadStatus.Incomplete;
        }

        foreach (ObservationReceipt receipt in found)
        {
            receipts.Add(receipt.Id, receipt);
        }

        return IngestionDataRightsEvidenceGraphLoadStatus.Succeeded;
    }

    private static int GraphRecordCount(
        ReservationDispatch[] dispatches,
        ChangeProposal[] proposals,
        int receiptCount) =>
        checked(1 + dispatches.Length + proposals.Length + receiptCount);

    private static IQueryable<T> Query<T>(
        DbSet<T> records,
        bool trackChanges)
        where T : class =>
        trackChanges ? records : records.AsNoTracking();

    private static void AddIfPresent(HashSet<Guid> values, Guid? value)
    {
        if (value is { } id && id != Guid.Empty)
        {
            values.Add(id);
        }
    }
}

internal sealed record IngestionDataRightsEvidenceGraph(
    IReadOnlyCollection<ChangeProposal> Proposals,
    IReadOnlyCollection<ReservationDispatch> Dispatches,
    IReadOnlyCollection<ObservationReceipt> Receipts,
    IReadOnlyCollection<ObservationReprocessingAttempt> Attempts,
    IReadOnlyCollection<ObservationReprocessingOutput> Outputs)
{
    public int RecordCount =>
        checked(
            1 +
            this.Proposals.Count +
            this.Dispatches.Count +
            this.Receipts.Count +
            this.Attempts.Count +
            this.Outputs.Count);
}

internal sealed record IngestionDataRightsEvidenceGraphLoadResult(
    IngestionDataRightsEvidenceGraphLoadStatus Status,
    IngestionDataRightsEvidenceGraph? Graph)
{
    public static IngestionDataRightsEvidenceGraphLoadResult Succeeded(
        IngestionDataRightsEvidenceGraph graph) =>
        new(IngestionDataRightsEvidenceGraphLoadStatus.Succeeded, graph);

    public static IngestionDataRightsEvidenceGraphLoadResult TooLarge() =>
        new(IngestionDataRightsEvidenceGraphLoadStatus.TooLarge, Graph: null);

    public static IngestionDataRightsEvidenceGraphLoadResult Incomplete() =>
        new(IngestionDataRightsEvidenceGraphLoadStatus.Incomplete, Graph: null);
}

internal enum IngestionDataRightsEvidenceGraphLoadStatus
{
    Succeeded = 1,
    TooLarge = 2,
    Incomplete = 3
}
