namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using Microsoft.EntityFrameworkCore;

internal sealed class RawPayloadRetentionRepository(
    IngestionDbContext dbContext,
    IRetentionFenceRepository retentionFence)
    : IRawPayloadRetentionRepository
{
    public async Task<IReadOnlyList<RawPayloadPurgeClaimCandidate>>
        FindClaimCandidatesAsync(
        Guid claimId,
        DateTimeOffset nowUtc,
        DateTimeOffset staleClaimBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        RawPayloadClaimIdentity[] identities = await this.EligibleReceipts(
                claimId,
                nowUtc,
                staleClaimBeforeUtc)
            .AsNoTracking()
            .OrderBy(receipt => receipt.RawPayloadRetainUntilUtc)
            .ThenBy(receipt => receipt.Id)
            .Take(batchSize)
            .Select(receipt => new RawPayloadClaimIdentity(
                receipt.Id,
                receipt.ScopeId,
                receipt.ConnectionId,
                receipt.ExternalId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return identities
            .Select(identity => new RawPayloadPurgeClaimCandidate(
                identity.ReceiptId,
                identity.ConnectionId,
                ReservationOperationIdentity.CreateSourceLinkId(
                    identity.ScopeId,
                    identity.ConnectionId,
                    identity.ExternalId)))
            .ToArray();
    }

    public async Task<IReadOnlyList<RawPayloadPurgeCandidate>>
        ClaimSelectedAsync(
        IReadOnlyCollection<Guid> receiptIds,
        Guid claimId,
        DateTimeOffset nowUtc,
        DateTimeOffset staleClaimBeforeUtc,
        CancellationToken cancellationToken)
    {
        Guid[] selectedIds = receiptIds.Distinct().ToArray();
        if (selectedIds.Length == 0)
        {
            return [];
        }

        List<ObservationReceipt> receipts = await this.EligibleReceipts(
                claimId,
                nowUtc,
                staleClaimBeforeUtc)
            .Where(receipt => selectedIds.Contains(receipt.Id))
            .OrderBy(receipt => receipt.RawPayloadRetainUntilUtc)
            .ThenBy(receipt => receipt.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (Guid propertyId in receipts.Select(receipt => receipt.PropertyId).Distinct())
        {
            if (!await retentionFence.TryAdvanceAsync(propertyId, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new InvalidOperationException("A selected raw payload has no known property retention fence.");
            }
        }

        foreach (ObservationReceipt receipt in receipts)
        {
            if (receipt.BeginRawPayloadPurge(claimId, nowUtc, staleClaimBeforeUtc).IsFailure)
            {
                throw new InvalidOperationException("A selected raw payload could not accept its purge claim.");
            }
        }

        return receipts
            .Select(receipt => new RawPayloadPurgeCandidate(
                receipt.Id,
                receipt.RawPayloadFileId,
                receipt.ConnectionId))
            .ToArray();
    }

    private IQueryable<ObservationReceipt> EligibleReceipts(
        Guid claimId,
        DateTimeOffset nowUtc,
        DateTimeOffset staleClaimBeforeUtc) =>
        dbContext.ObservationReceipts.Where(receipt =>
            receipt.AnonymisedAtUtc == null &&
            (receipt.State == ObservationReceiptState.Processed ||
             receipt.State == ObservationReceiptState.Rejected) &&
            receipt.RawPayloadRetainUntilUtc <= nowUtc &&
            !(receipt.ActiveReprocessingAttemptId != null &&
              receipt.ReprocessingReservationExpiresAtUtc > nowUtc) &&
            !dbContext.LegalHolds.Any(legalHold =>
                legalHold.ScopeId == receipt.ScopeId &&
                legalHold.PropertyId == receipt.PropertyId &&
                legalHold.State == LegalHoldState.Active) &&
            !dbContext.ChangeProposals.Any(proposal =>
                proposal.ScopeId == receipt.ScopeId &&
                proposal.ReceiptId == receipt.Id &&
                (proposal.State == ChangeProposalState.Pending ||
                 proposal.State == ChangeProposalState.Applying)) &&
            (receipt.RawPayloadRetentionState ==
                RawPayloadRetentionState.Available ||
             (receipt.RawPayloadRetentionState ==
                    RawPayloadRetentionState.Purging &&
              (receipt.RawPayloadPurgeClaimId == claimId ||
               receipt.RawPayloadPurgeStartedAtUtc == null ||
               receipt.RawPayloadPurgeStartedAtUtc <=
                    staleClaimBeforeUtc))));

    private sealed record RawPayloadClaimIdentity(
        Guid ReceiptId,
        string ScopeId,
        Guid ConnectionId,
        string ExternalId);
}
