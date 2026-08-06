namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionSourceGraphLocator(IngestionDbContext dbContext)
    : IIngestionSourceGraphLocator
{
    public async Task<IngestionSourceGraphCoordinate?> FindReceiptAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        ReceiptIdentity? identity = await dbContext.ObservationReceipts
            .AsNoTracking()
            .Where(receipt => receipt.Id == receiptId)
            .Select(receipt => new ReceiptIdentity(
                receipt.Id,
                receipt.ScopeId,
                receipt.ConnectionId,
                receipt.ExternalId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return ToCoordinate(identity);
    }

    public async Task<IngestionSourceGraphCoordinate?> FindProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        ReceiptIdentity? identity = await (
                from proposal in dbContext.ChangeProposals.AsNoTracking()
                join receipt in dbContext.ObservationReceipts.AsNoTracking()
                    on proposal.ReceiptId equals receipt.Id
                where proposal.Id == proposalId
                select new ReceiptIdentity(
                    proposal.Id,
                    receipt.ScopeId,
                    receipt.ConnectionId,
                    receipt.ExternalId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return ToCoordinate(identity);
    }

    public Task<IngestionSourceGraphCoordinate?> FindDispatchAsync(
        Guid dispatchId,
        CancellationToken cancellationToken) =>
        dbContext.ReservationDispatches
            .AsNoTracking()
            .Where(dispatch => dispatch.Id == dispatchId)
            .Select(dispatch => new IngestionSourceGraphCoordinate(
                dispatch.Id,
                dispatch.ConnectionId,
                dispatch.SourceLinkId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IngestionSourceGraphCoordinate?> FindReprocessingAttemptAsync(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        ReceiptIdentity? identity = await (
                from attempt in dbContext.ObservationReprocessingAttempts
                    .AsNoTracking()
                join receipt in dbContext.ObservationReceipts.AsNoTracking()
                    on attempt.SourceReceiptId equals receipt.Id
                where attempt.Id == attemptId
                select new ReceiptIdentity(
                    attempt.Id,
                    receipt.ScopeId,
                    receipt.ConnectionId,
                    receipt.ExternalId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return ToCoordinate(identity);
    }

    public Task<IngestionSourceGraphCoordinate?> FindSourceLinkAsync(
        Guid sourceLinkId,
        CancellationToken cancellationToken) =>
        dbContext.ReservationSourceLinks
            .AsNoTracking()
            .Where(link => link.Id == sourceLinkId)
            .Select(link => new IngestionSourceGraphCoordinate(
                link.Id,
                link.ConnectionId,
                link.Id))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<IngestionSourceGraphCoordinate?> FindAcceptedCancellationAsync(
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.ReservationDispatches
            .AsNoTracking()
            .Where(dispatch =>
                dispatch.ReservationId == reservationId &&
                dispatch.Kind == ReservationDispatchKind.Cancel &&
                dispatch.State == ReservationDispatchState.Accepted)
            .Select(dispatch => new IngestionSourceGraphCoordinate(
                dispatch.Id,
                dispatch.ConnectionId,
                dispatch.SourceLinkId))
            .SingleOrDefaultAsync(cancellationToken);

    private static IngestionSourceGraphCoordinate? ToCoordinate(
        ReceiptIdentity? identity) =>
        identity is null
            ? null
            : new(
                identity.RecordId,
                identity.ConnectionId,
                ReservationOperationIdentity.CreateSourceLinkId(
                    identity.ScopeId,
                    identity.ConnectionId,
                    identity.ExternalId));

    private sealed record ReceiptIdentity(
        Guid RecordId,
        string ScopeId,
        Guid ConnectionId,
        string ExternalId);
}
