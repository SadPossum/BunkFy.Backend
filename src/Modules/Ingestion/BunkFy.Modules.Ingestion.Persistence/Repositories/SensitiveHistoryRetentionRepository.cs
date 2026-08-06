namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using Microsoft.EntityFrameworkCore;

internal sealed class SensitiveHistoryRetentionRepository(
    IngestionDbContext dbContext,
    IRetentionFenceRepository retentionFence)
    : ISensitiveHistoryRetentionRepository
{
    public async Task<IReadOnlyList<SensitiveHistoryRedactionCandidate>>
        FindRedactionCandidatesAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ProposalIdentity[] proposals = await (
                from proposal in this.EligibleProposals(nowUtc).AsNoTracking()
                join receipt in dbContext.ObservationReceipts.AsNoTracking()
                    on proposal.ReceiptId equals receipt.Id
                orderby proposal.SensitiveDataRetainUntilUtc, proposal.Id
                select new ProposalIdentity(
                    proposal.Id,
                    receipt.ScopeId,
                    receipt.ConnectionId,
                    receipt.ExternalId,
                    proposal.SensitiveDataRetainUntilUtc!.Value))
            .Take(batchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        SensitiveHistoryRedactionCandidate[] dispatches =
            await this.EligibleDispatches(nowUtc)
            .AsNoTracking()
            .OrderBy(dispatch => dispatch.SensitiveDataRetainUntilUtc)
            .ThenBy(dispatch => dispatch.Id)
            .Take(batchSize)
            .Select(dispatch => new SensitiveHistoryRedactionCandidate(
                SensitiveHistoryRecordKind.Dispatch,
                dispatch.Id,
                dispatch.ConnectionId,
                dispatch.SourceLinkId,
                dispatch.SensitiveDataRetainUntilUtc!.Value))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return proposals
            .Select(proposal => new SensitiveHistoryRedactionCandidate(
                SensitiveHistoryRecordKind.Proposal,
                proposal.ProposalId,
                proposal.ConnectionId,
                ReservationOperationIdentity.CreateSourceLinkId(
                    proposal.ScopeId,
                    proposal.ConnectionId,
                    proposal.ExternalId),
                proposal.RetainUntilUtc))
            .Concat(dispatches)
            .OrderBy(candidate => candidate.RetainUntilUtc)
            .ThenBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.RecordId)
            .Take(batchSize)
            .ToArray();
    }

    public async Task<SensitiveHistoryRedactionBatchResult>
        RedactSelectedAsync(
        IReadOnlyCollection<Guid> proposalIds,
        IReadOnlyCollection<Guid> dispatchIds,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Guid[] selectedProposalIds = proposalIds.Distinct().ToArray();
        Guid[] selectedDispatchIds = dispatchIds.Distinct().ToArray();
        List<ChangeProposal> proposals = selectedProposalIds.Length == 0
            ? []
            : await this.EligibleProposals(nowUtc)
                .Where(proposal => selectedProposalIds.Contains(proposal.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        List<ReservationDispatch> dispatches =
            selectedDispatchIds.Length == 0
                ? []
                : await this.EligibleDispatches(nowUtc)
                    .Where(dispatch =>
                        selectedDispatchIds.Contains(dispatch.Id))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

        foreach (Guid propertyId in proposals
                     .Select(proposal => proposal.PropertyId)
                     .Concat(dispatches.Select(dispatch => dispatch.PropertyId))
                     .Distinct())
        {
            if (!await retentionFence.TryAdvanceAsync(propertyId, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new InvalidOperationException("Selected sensitive history has no known property retention fence.");
            }
        }

        int proposalCount = 0;
        int dispatchCount = 0;
        foreach (ChangeProposal proposal in proposals)
        {
            if (proposal.RedactSensitiveData(nowUtc).IsFailure)
            {
                throw new InvalidOperationException(
                    "A selected proposal could not be redacted.");
            }

            proposalCount++;
        }

        foreach (ReservationDispatch dispatch in dispatches)
        {
            if (dispatch.RedactSensitiveData(nowUtc).IsFailure)
            {
                throw new InvalidOperationException(
                    "A selected reservation dispatch could not be redacted.");
            }

            dispatchCount++;
        }

        return new SensitiveHistoryRedactionBatchResult(proposalCount, dispatchCount);
    }

    private IQueryable<ChangeProposal> EligibleProposals(
        DateTimeOffset nowUtc) =>
        dbContext.ChangeProposals.Where(proposal =>
            proposal.Diff != null &&
            proposal.SensitiveDataRedactedAtUtc == null &&
            proposal.SensitiveDataRetainUntilUtc <= nowUtc &&
            !dbContext.LegalHolds.Any(legalHold =>
                legalHold.ScopeId == proposal.ScopeId &&
                legalHold.PropertyId == proposal.PropertyId &&
                legalHold.State == LegalHoldState.Active));

    private IQueryable<ReservationDispatch> EligibleDispatches(
        DateTimeOffset nowUtc) =>
        dbContext.ReservationDispatches.Where(dispatch =>
            dispatch.NormalizedSnapshot != null &&
            dispatch.SensitiveDataRedactedAtUtc == null &&
            dispatch.SensitiveDataRetainUntilUtc <= nowUtc &&
            !dbContext.LegalHolds.Any(legalHold =>
                legalHold.ScopeId == dispatch.ScopeId &&
                legalHold.PropertyId == dispatch.PropertyId &&
                legalHold.State == LegalHoldState.Active));

    private sealed record ProposalIdentity(
        Guid ProposalId,
        string ScopeId,
        Guid ConnectionId,
        string ExternalId,
        DateTimeOffset RetainUntilUtc);
}
