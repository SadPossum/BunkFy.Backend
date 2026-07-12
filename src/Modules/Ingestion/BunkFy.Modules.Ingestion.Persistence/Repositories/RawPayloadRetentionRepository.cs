namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

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
    public async Task<IReadOnlyList<RawPayloadPurgeCandidate>> ClaimBatchAsync(
        Guid claimId,
        DateTimeOffset nowUtc,
        DateTimeOffset staleClaimBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        List<ObservationReceipt> receipts = await dbContext.ObservationReceipts
            .Where(receipt =>
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
                (receipt.RawPayloadRetentionState == RawPayloadRetentionState.Available ||
                 (receipt.RawPayloadRetentionState == RawPayloadRetentionState.Purging &&
                  (receipt.RawPayloadPurgeClaimId == claimId ||
                   receipt.RawPayloadPurgeStartedAtUtc == null ||
                   receipt.RawPayloadPurgeStartedAtUtc <= staleClaimBeforeUtc))))
            .OrderBy(receipt => receipt.RawPayloadRetainUntilUtc)
            .ThenBy(receipt => receipt.Id)
            .Take(batchSize)
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
}
