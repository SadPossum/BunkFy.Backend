namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionRetentionStatusReader(IngestionDbContext dbContext)
    : IIngestionRetentionStatusReader
{
    public async Task<IngestionRetentionBacklog> ReadRawPayloadBacklogAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        IQueryable<ObservationReceipt> baseQuery = dbContext.ObservationReceipts
            .AsNoTracking()
            .Where(receipt =>
                receipt.AnonymisedAtUtc == null &&
                (receipt.State == ObservationReceiptState.Processed ||
                 receipt.State == ObservationReceiptState.Rejected) &&
                receipt.RawPayloadRetainUntilUtc <= nowUtc &&
                !(receipt.ActiveReprocessingAttemptId != null &&
                  receipt.ReprocessingReservationExpiresAtUtc > nowUtc) &&
                !dbContext.ChangeProposals.Any(proposal =>
                    proposal.ScopeId == receipt.ScopeId &&
                    proposal.ReceiptId == receipt.Id &&
                    (proposal.State == ChangeProposalState.Pending ||
                     proposal.State == ChangeProposalState.Applying)) &&
                receipt.RawPayloadRetentionState ==
                    RawPayloadRetentionState.Available);
        int eligible = await baseQuery.CountAsync(
            receipt => !dbContext.LegalHolds.Any(hold =>
                hold.PropertyId == receipt.PropertyId &&
                hold.State == LegalHoldState.Active),
            cancellationToken).ConfigureAwait(false);
        int blocked = await baseQuery.CountAsync(
            receipt => dbContext.LegalHolds.Any(hold =>
                hold.PropertyId == receipt.PropertyId &&
                hold.State == LegalHoldState.Active),
            cancellationToken).ConfigureAwait(false);
        DateTimeOffset? earliestHold = await this.EarliestBlockingHoldAsync(
            baseQuery.Select(receipt => receipt.PropertyId),
            cancellationToken).ConfigureAwait(false);
        return new(eligible, blocked, earliestHold);
    }

    public async Task<IngestionRetentionBacklog> ReadSensitiveHistoryBacklogAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        IQueryable<ChangeProposal> proposals = dbContext.ChangeProposals
            .AsNoTracking()
            .Where(proposal =>
                proposal.Diff != null &&
                proposal.SensitiveDataRedactedAtUtc == null &&
                proposal.SensitiveDataRetainUntilUtc <= nowUtc);
        IQueryable<ReservationDispatch> dispatches =
            dbContext.ReservationDispatches
                .AsNoTracking()
                .Where(dispatch =>
                    dispatch.NormalizedSnapshot != null &&
                    dispatch.SensitiveDataRedactedAtUtc == null &&
                    dispatch.SensitiveDataRetainUntilUtc <= nowUtc);
        int eligible = await proposals.CountAsync(
                proposal => !dbContext.LegalHolds.Any(hold =>
                    hold.PropertyId == proposal.PropertyId &&
                    hold.State == LegalHoldState.Active),
                cancellationToken).ConfigureAwait(false) +
            await dispatches.CountAsync(
                dispatch => !dbContext.LegalHolds.Any(hold =>
                    hold.PropertyId == dispatch.PropertyId &&
                    hold.State == LegalHoldState.Active),
                cancellationToken).ConfigureAwait(false);
        int blocked = await proposals.CountAsync(
                proposal => dbContext.LegalHolds.Any(hold =>
                    hold.PropertyId == proposal.PropertyId &&
                    hold.State == LegalHoldState.Active),
                cancellationToken).ConfigureAwait(false) +
            await dispatches.CountAsync(
                dispatch => dbContext.LegalHolds.Any(hold =>
                    hold.PropertyId == dispatch.PropertyId &&
                    hold.State == LegalHoldState.Active),
                cancellationToken).ConfigureAwait(false);
        IQueryable<Guid> blockedPropertyIds = proposals
            .Select(proposal => proposal.PropertyId)
            .Concat(dispatches.Select(dispatch => dispatch.PropertyId));
        DateTimeOffset? earliestHold = await this.EarliestBlockingHoldAsync(
            blockedPropertyIds,
            cancellationToken).ConfigureAwait(false);
        return new(eligible, blocked, earliestHold);
    }

    private Task<DateTimeOffset?> EarliestBlockingHoldAsync(
        IQueryable<Guid> propertyIds,
        CancellationToken cancellationToken) =>
        dbContext.LegalHolds
            .AsNoTracking()
            .Where(hold =>
                hold.State == LegalHoldState.Active &&
                propertyIds.Contains(hold.PropertyId))
            .Select(hold => (DateTimeOffset?)hold.PlacedAtUtc)
            .MinAsync(cancellationToken);
}
