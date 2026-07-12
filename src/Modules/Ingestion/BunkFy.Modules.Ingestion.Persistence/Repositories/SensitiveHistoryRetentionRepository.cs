namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

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
    public async Task<SensitiveHistoryRedactionBatchResult> RedactBatchAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        List<ChangeProposal> proposals = await dbContext.ChangeProposals
            .Where(proposal =>
                proposal.Diff != null &&
                proposal.SensitiveDataRedactedAtUtc == null &&
                proposal.SensitiveDataRetainUntilUtc <= nowUtc &&
                !dbContext.LegalHolds.Any(legalHold =>
                    legalHold.ScopeId == proposal.ScopeId &&
                    legalHold.PropertyId == proposal.PropertyId &&
                    legalHold.State == LegalHoldState.Active))
            .OrderBy(proposal => proposal.SensitiveDataRetainUntilUtc)
            .ThenBy(proposal => proposal.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<ReservationDispatch> dispatches = await dbContext.ReservationDispatches
            .Where(dispatch =>
                dispatch.NormalizedSnapshot != null &&
                dispatch.SensitiveDataRedactedAtUtc == null &&
                dispatch.SensitiveDataRetainUntilUtc <= nowUtc &&
                !dbContext.LegalHolds.Any(legalHold =>
                    legalHold.ScopeId == dispatch.ScopeId &&
                    legalHold.PropertyId == dispatch.PropertyId &&
                    legalHold.State == LegalHoldState.Active))
            .OrderBy(dispatch => dispatch.SensitiveDataRetainUntilUtc)
            .ThenBy(dispatch => dispatch.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        SensitiveHistoryCandidate[] selected = proposals
            .Select(proposal => new SensitiveHistoryCandidate(
                proposal.SensitiveDataRetainUntilUtc!.Value,
                proposal.Id,
                proposal,
                Dispatch: null))
            .Concat(dispatches.Select(dispatch => new SensitiveHistoryCandidate(
                dispatch.SensitiveDataRetainUntilUtc!.Value,
                dispatch.Id,
                Proposal: null,
                Dispatch: dispatch)))
            .OrderBy(candidate => candidate.RetainUntilUtc)
            .ThenBy(candidate => candidate.Proposal is null ? 1 : 0)
            .ThenBy(candidate => candidate.Id)
            .Take(batchSize)
            .ToArray();

        foreach (Guid propertyId in selected
                     .Select(candidate => candidate.Proposal?.PropertyId ?? candidate.Dispatch!.PropertyId)
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
        foreach (SensitiveHistoryCandidate candidate in selected)
        {
            if (candidate.Proposal is not null)
            {
                if (candidate.Proposal.RedactSensitiveData(nowUtc).IsFailure)
                {
                    throw new InvalidOperationException("A selected proposal could not be redacted.");
                }

                proposalCount++;
            }
            else
            {
                if (candidate.Dispatch!.RedactSensitiveData(nowUtc).IsFailure)
                {
                    throw new InvalidOperationException("A selected reservation dispatch could not be redacted.");
                }

                dispatchCount++;
            }
        }

        return new SensitiveHistoryRedactionBatchResult(proposalCount, dispatchCount);
    }

    private sealed record SensitiveHistoryCandidate(
        DateTimeOffset RetainUntilUtc,
        Guid Id,
        ChangeProposal? Proposal,
        ReservationDispatch? Dispatch);
}
