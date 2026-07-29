namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationRetentionCandidateRepository(
    ReservationsDbContext dbContext)
    : IReservationRetentionCandidateRepository
{
    public async Task<ReservationRetentionScanPage> ScanAsync(
        long afterProjectionOrdinal,
        int limit,
        CancellationToken cancellationToken)
    {
        if (afterProjectionOrdinal < 0 || limit < 1)
        {
            throw new ArgumentOutOfRangeException(
                limit < 1
                    ? nameof(limit)
                    : nameof(afterProjectionOrdinal));
        }

        IQueryable<Reservation> candidates = this.QueryCandidates()
            .Where(reservation =>
                reservation.ProjectionOrdinal >
                    afterProjectionOrdinal)
            .OrderBy(reservation =>
                reservation.ProjectionOrdinal)
            .Take(checked(limit + 1));
        ReservationRetentionHead[] rows = await this.ProjectHeads(
                candidates)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        bool reachedEnd = rows.Length <= limit;
        ReservationRetentionHead[] page =
            rows.Take(limit).ToArray();
        return new(
            await this.LoadSnapshotsAsync(
                page,
                cancellationToken).ConfigureAwait(false),
            reachedEnd);
    }

    public async Task<ReservationRetentionCandidateSnapshot?> LoadAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        IQueryable<Reservation> candidates = this.QueryCandidates()
            .Where(reservation =>
                reservation.PropertyId == propertyId &&
                reservation.Id == reservationId);
        ReservationRetentionHead? head =
            await this.ProjectHeads(candidates)
                .SingleOrDefaultAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        if (head is null)
        {
            return null;
        }

        return (await this.LoadSnapshotsAsync(
                [head],
                cancellationToken).ConfigureAwait(false))
            .Single();
    }

    private IQueryable<Reservation> QueryCandidates() =>
        dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                !reservation.IsAnonymised &&
                reservation.TerminalAtUtc != null &&
                (reservation.Status ==
                    ReservationState.AllocationRejected ||
                 reservation.Status == ReservationState.Cancelled ||
                 reservation.Status == ReservationState.NoShow ||
                 reservation.Status == ReservationState.CheckedOut));

    private IQueryable<ReservationRetentionHead> ProjectHeads(
        IQueryable<Reservation> candidates) =>
        candidates.Select(reservation => new ReservationRetentionHead(
                reservation.Id,
                reservation.PropertyId,
                reservation.Version,
                reservation.DetailsRevision,
                reservation.ProjectionOrdinal,
                reservation.Status,
                reservation.PendingAllocationAmendmentId != null,
                reservation.IsAnonymised,
                reservation.TerminalAtUtc,
                dbContext.DataHolds.Count(hold =>
                    hold.PropertyId == reservation.PropertyId &&
                    hold.ReservationId == reservation.Id &&
                    hold.State == ReservationDataHoldState.Active),
                dbContext.DataHolds
                    .Where(hold =>
                        hold.PropertyId == reservation.PropertyId &&
                        hold.ReservationId == reservation.Id &&
                        hold.State ==
                            ReservationDataHoldState.Active)
                    .Select(hold =>
                        (DateTimeOffset?)hold.PlacedAtUtc)
                    .Min(),
                dbContext.ProcessingRestrictionProjections
                    .Where(projection =>
                        projection.PropertyId ==
                            reservation.PropertyId &&
                        projection.ReservationId == reservation.Id)
                    .Select(projection =>
                        (int?)projection.ContractVersion)
                    .SingleOrDefault()));

    private async Task<
        IReadOnlyList<ReservationRetentionCandidateSnapshot>>
        LoadSnapshotsAsync(
            IReadOnlyList<ReservationRetentionHead> heads,
            CancellationToken cancellationToken)
    {
        if (heads.Count == 0)
        {
            return [];
        }

        Guid[] propertyIds = heads
            .Select(head => head.PropertyId)
            .Distinct()
            .ToArray();
        ReservationPropertyProjection[] properties =
            await dbContext.PropertyProjections
                .AsNoTracking()
                .Include(property => property.GovernancePolicy)
                .ThenInclude(policy =>
                    policy!.Acknowledgements)
                .Where(property => propertyIds.Contains(property.Id))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        Dictionary<Guid, ReservationRetentionPropertySnapshot>
            propertyById = properties.ToDictionary(
                property => property.Id,
                MapProperty);

        return heads
            .Select(head =>
                new ReservationRetentionCandidateSnapshot(
                    head.ReservationId,
                    head.PropertyId,
                    head.ReservationVersion,
                    head.DetailsRevision,
                    head.ProjectionOrdinal,
                    head.Status,
                    head.HasPendingAllocationAmendment,
                    head.IsAnonymised,
                    head.TerminalAtUtc,
                    head.ActiveHoldCount,
                    head.EarliestHoldPlacedAtUtc,
                    head.ProcessingRestrictionContractVersion,
                    propertyById.GetValueOrDefault(
                        head.PropertyId)))
            .ToArray();
    }

    private static ReservationRetentionPropertySnapshot MapProperty(
        ReservationPropertyProjection property) =>
        new(
            property.IsKnown,
            property.IsActive,
            property.ProcessingStatus,
            property.TopologySourceVersion,
            property.PolicySourceVersion,
            MapPolicy(property.GovernancePolicy));

    private static PropertyGovernancePolicyBinding? MapPolicy(
        ReservationPropertyPolicyBinding? policy) =>
        policy is null
            ? null
            : new(
                policy.OperatingCountryCode,
                policy.PolicyId,
                policy.PolicyVersion,
                policy.DataRegionId,
                policy.TransferProfileId,
                policy.RetentionPolicyId,
                policy.RetentionPolicyVersion,
                policy.ContentSha256,
                policy.PolicyEffectiveAtUtc,
                policy.PolicyExpiresAtUtc,
                policy.ActivatedAtUtc,
                policy.Acknowledgements.Select(acknowledgement =>
                    new PropertyGovernanceAcknowledgement(
                        acknowledgement.AcknowledgementId,
                        acknowledgement.AcknowledgementVersion))
                    .ToArray());

    private sealed record ReservationRetentionHead(
        Guid ReservationId,
        Guid PropertyId,
        long ReservationVersion,
        long DetailsRevision,
        long ProjectionOrdinal,
        ReservationState Status,
        bool HasPendingAllocationAmendment,
        bool IsAnonymised,
        DateTimeOffset? TerminalAtUtc,
        int ActiveHoldCount,
        DateTimeOffset? EarliestHoldPlacedAtUtc,
        int? ProcessingRestrictionContractVersion);
}
