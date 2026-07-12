namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Linq.Expressions;
using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using Microsoft.EntityFrameworkCore;

internal sealed class ChangeProposalReader(IngestionDbContext dbContext) : IChangeProposalReader
{
    private static readonly Expression<Func<ChangeProposal, ChangeProposalDto>> Projection = proposal => new(
        proposal.Id,
        proposal.PropertyId,
        proposal.ConnectionId,
        proposal.ReceiptId,
        proposal.ReservationId,
        proposal.BaseReservationDetailsRevision,
        proposal.ReasonCode,
        proposal.Diff,
        proposal.SensitiveDataRedactedAtUtc.HasValue
            ? SensitiveHistoryStatus.Redacted
            : SensitiveHistoryStatus.Available,
        proposal.SensitiveDataRetainUntilUtc,
        proposal.SensitiveDataRedactedAtUtc,
        (ChangeProposalStatus)(int)proposal.State,
        proposal.DecisionActor,
        proposal.DecisionReason,
        proposal.ProductOperationId,
        proposal.Version,
        proposal.CreatedAtUtc,
        proposal.DecidedAtUtc,
        proposal.CompletedAtUtc);

    private static readonly Expression<Func<ChangeProposal, ChangeProposalSummaryDto>> SummaryProjection =
        proposal => new(
            proposal.Id,
            proposal.PropertyId,
            proposal.ConnectionId,
            proposal.ReceiptId,
            proposal.ReservationId,
            proposal.BaseReservationDetailsRevision,
            proposal.ReasonCode,
            proposal.SensitiveDataRedactedAtUtc.HasValue
                ? SensitiveHistoryStatus.Redacted
                : SensitiveHistoryStatus.Available,
            proposal.SensitiveDataRetainUntilUtc,
            proposal.SensitiveDataRedactedAtUtc,
            (ChangeProposalStatus)(int)proposal.State,
            proposal.DecisionActor,
            proposal.DecisionReason,
            proposal.ProductOperationId,
            proposal.Version,
            proposal.CreatedAtUtc,
            proposal.DecidedAtUtc,
            proposal.CompletedAtUtc);

    public Task<ChangeProposalDto?> GetAsync(
        Guid propertyId,
        Guid proposalId,
        CancellationToken cancellationToken) => dbContext.ChangeProposals
        .AsNoTracking()
        .Where(proposal => proposal.PropertyId == propertyId && proposal.Id == proposalId)
        .Select(Projection)
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<ChangeProposalListResponse> ListAsync(
        Guid propertyId,
        ChangeProposalStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<ChangeProposal> query = dbContext.ChangeProposals.AsNoTracking()
            .Where(proposal => proposal.PropertyId == propertyId);
        if (status.HasValue && status.Value != ChangeProposalStatus.Unknown)
        {
            ChangeProposalState state = ToDomain(status.Value);
            query = query.Where(proposal => proposal.State == state);
        }

        long totalCount = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        ChangeProposalSummaryDto[] proposals = await query
            .OrderByDescending(proposal => proposal.CreatedAtUtc)
            .ThenBy(proposal => proposal.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .Select(SummaryProjection)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(proposals, pageRequest.Page, pageRequest.PageSize, totalCount);
    }

    private static ChangeProposalState ToDomain(ChangeProposalStatus status) => status switch
    {
        ChangeProposalStatus.Pending => ChangeProposalState.Pending,
        ChangeProposalStatus.Applying => ChangeProposalState.Applying,
        ChangeProposalStatus.Applied => ChangeProposalState.Applied,
        ChangeProposalStatus.Rejected => ChangeProposalState.Rejected,
        ChangeProposalStatus.Superseded => ChangeProposalState.Superseded,
        ChangeProposalStatus.Stale => ChangeProposalState.Stale,
        ChangeProposalStatus.Failed => ChangeProposalState.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported proposal status filter.")
    };
}
