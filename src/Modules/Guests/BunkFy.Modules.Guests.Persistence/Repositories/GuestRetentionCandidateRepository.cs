namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestRetentionCandidateRepository(
    GuestsDbContext dbContext)
    : IGuestRetentionCandidateRepository
{
    public async Task<GuestRetentionScanPage> ScanAsync(
        long afterProjectionOrdinal,
        int limit,
        CancellationToken cancellationToken)
    {
        if (afterProjectionOrdinal < 0 || limit < 1)
        {
            throw new ArgumentOutOfRangeException(
                limit < 1 ? nameof(limit) : nameof(afterProjectionOrdinal));
        }

        GuestRetentionProfileHead[] rows = await dbContext.GuestProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.ProjectionOrdinal > afterProjectionOrdinal &&
                (profile.Status == GuestProfileState.Active ||
                 profile.Status == GuestProfileState.Archived))
            .OrderBy(profile => profile.ProjectionOrdinal)
            .Select(profile => new GuestRetentionProfileHead(
                profile.Id,
                profile.Version,
                profile.ProjectionOrdinal,
                profile.Status,
                profile.OriginPropertyId))
            .Take(checked(limit + 1))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        bool reachedEnd = rows.Length <= limit;
        GuestRetentionProfileHead[] page = rows.Take(limit).ToArray();
        IReadOnlyList<GuestRetentionCandidateSnapshot> candidates =
            await this.LoadSnapshotsAsync(
                page,
                cancellationToken).ConfigureAwait(false);
        return new(candidates, reachedEnd);
    }

    public async Task<GuestRetentionCandidateSnapshot?> LoadAsync(
        Guid guestId,
        CancellationToken cancellationToken)
    {
        GuestRetentionProfileHead? profile = await dbContext.GuestProfiles
            .AsNoTracking()
            .Where(item =>
                item.Id == guestId &&
                (item.Status == GuestProfileState.Active ||
                 item.Status == GuestProfileState.Archived))
            .Select(item => new GuestRetentionProfileHead(
                item.Id,
                item.Version,
                item.ProjectionOrdinal,
                item.Status,
                item.OriginPropertyId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        return (await this.LoadSnapshotsAsync(
                [profile],
                cancellationToken).ConfigureAwait(false))
            .Single();
    }

    private async Task<IReadOnlyList<GuestRetentionCandidateSnapshot>>
        LoadSnapshotsAsync(
            IReadOnlyList<GuestRetentionProfileHead> profiles,
            CancellationToken cancellationToken)
    {
        if (profiles.Count == 0)
        {
            return [];
        }

        Guid[] guestIds = profiles.Select(profile => profile.GuestId).ToArray();
        GuestRetentionStayAggregateRow[] stayRows =
            await dbContext.StayHistory
                .AsNoTracking()
                .Where(stay => guestIds.Contains(stay.GuestId))
                .GroupBy(stay => new
                {
                    stay.GuestId,
                    stay.PropertyId
                })
                .Select(group => new GuestRetentionStayAggregateRow(
                    group.Key.GuestId,
                    group.Key.PropertyId,
                    !group.Any(stay =>
                        stay.PropertyId == Guid.Empty ||
                        stay.ReservationId == Guid.Empty ||
                        stay.Role != GuestStayRole.Primary ||
                        (int)stay.Status <
                            (int)GuestStayStatus.PendingAllocation ||
                        (int)stay.Status >
                            (int)GuestStayStatus.CheckedOut ||
                        stay.ReservationVersion < 1 ||
                        stay.ProjectionContractVersion !=
                            GuestsModuleMetadata
                                .StayHistoryProjectionVersion),
                    group.Any(stay =>
                        stay.IsCurrentParticipant &&
                        (stay.Status ==
                            GuestStayStatus.PendingAllocation ||
                         stay.Status == GuestStayStatus.Confirmed ||
                         stay.Status ==
                            GuestStayStatus.CancellationPending ||
                         stay.Status == GuestStayStatus.CheckedIn ||
                         stay.Status == GuestStayStatus.NoShowPending ||
                         stay.Status ==
                            GuestStayStatus.CheckoutPending)),
                    group.Max(stay =>
                        stay.Status ==
                            GuestStayStatus.AllocationRejected ||
                        stay.Status == GuestStayStatus.Cancelled ||
                        stay.Status == GuestStayStatus.NoShow ||
                        stay.Status == GuestStayStatus.CheckedOut
                            ? (DateOnly?)stay.Departure
                            : null),
                    group.Max(stay =>
                        stay.Status ==
                            GuestStayStatus.AllocationRejected ||
                        stay.Status == GuestStayStatus.Cancelled ||
                        stay.Status == GuestStayStatus.NoShow ||
                        stay.Status == GuestStayStatus.CheckedOut
                            ? stay.NoShowBusinessDate
                            : null),
                    group.Max(stay =>
                        stay.Status ==
                            GuestStayStatus.AllocationRejected ||
                        stay.Status == GuestStayStatus.Cancelled ||
                        stay.Status == GuestStayStatus.NoShow ||
                        stay.Status == GuestStayStatus.CheckedOut
                            ? stay.CheckedOutBusinessDate
                            : null)))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        GuestRetentionHoldRow[] holdRows = await dbContext.DataHolds
            .AsNoTracking()
            .Where(hold =>
                guestIds.Contains(hold.GuestId) &&
                hold.State == GuestDataHoldState.Active)
            .Select(hold => new GuestRetentionHoldRow(
                hold.GuestId,
                hold.PropertyId,
                hold.PlacedAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid[] propertyIds = profiles
            .Select(profile => profile.OriginPropertyId)
            .Concat(stayRows.Select(stay => stay.PropertyId))
            .Concat(holdRows.Select(hold => hold.PropertyId))
            .Distinct()
            .ToArray();
        GuestPropertyProjection[] properties =
            await dbContext.PropertyProjections
                .AsNoTracking()
                .Include(property => property.GovernancePolicy)
                .ThenInclude(policy => policy!.Acknowledgements)
                .Where(property => propertyIds.Contains(property.Id))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        Dictionary<Guid, GuestRetentionPropertySnapshot> propertyIndex =
            properties.ToDictionary(
                property => property.Id,
                property => new GuestRetentionPropertySnapshot(
                    property.Id,
                    property.IsKnown,
                    property.Status,
                    property.ProcessingStatus,
                    property.TimeZoneId,
                    property.TopologySourceVersion,
                    property.PolicySourceVersion,
                    property.GovernancePolicy.ToContract()));

        ILookup<Guid, GuestRetentionStayAggregateRow> staysByGuest =
            stayRows.ToLookup(stay => stay.GuestId);
        ILookup<Guid, GuestRetentionHoldRow> holdsByGuest =
            holdRows.ToLookup(hold => hold.GuestId);
        return profiles.Select(profile =>
        {
            GuestRetentionStaySnapshot[] stays = staysByGuest[profile.GuestId]
                .Select(stay => new GuestRetentionStaySnapshot(
                    stay.PropertyId,
                    stay.ProjectionSupported,
                    stay.HasOperationalStay,
                    Latest(
                        stay.LatestDeparture,
                        stay.LatestNoShow,
                        stay.LatestCheckout)))
                .OrderBy(stay => stay.PropertyId)
                .ToArray();
            GuestRetentionHoldSnapshot[] holds =
                holdsByGuest[profile.GuestId]
                    .Select(hold => new GuestRetentionHoldSnapshot(
                        hold.PropertyId,
                        hold.PlacedAtUtc))
                    .OrderBy(hold => hold.PropertyId)
                    .ThenBy(hold => hold.PlacedAtUtc)
                    .ToArray();
            Guid[] affectedPropertyIds = stays
                .Select(stay => stay.PropertyId)
                .Append(profile.OriginPropertyId)
                .Concat(holds.Select(hold => hold.PropertyId))
                .Distinct()
                .OrderBy(propertyId => propertyId)
                .ToArray();
            GuestRetentionPropertySnapshot[] snapshots =
                affectedPropertyIds
                    .Where(propertyIndex.ContainsKey)
                    .Select(propertyId => propertyIndex[propertyId])
                    .ToArray();
            return new GuestRetentionCandidateSnapshot(
                profile.GuestId,
                profile.GuestVersion,
                profile.ProjectionOrdinal,
                profile.GuestState,
                profile.OriginPropertyId,
                stays,
                holds,
                snapshots);
        }).ToArray();
    }

    private static DateOnly? Latest(
        DateOnly? first,
        DateOnly? second,
        DateOnly? third)
    {
        DateOnly? result = first;
        if (second.HasValue &&
            (!result.HasValue || second.Value > result.Value))
        {
            result = second;
        }

        if (third.HasValue &&
            (!result.HasValue || third.Value > result.Value))
        {
            result = third;
        }

        return result;
    }

    private sealed record GuestRetentionProfileHead(
        Guid GuestId,
        long GuestVersion,
        long ProjectionOrdinal,
        GuestProfileState GuestState,
        Guid OriginPropertyId);

    private sealed record GuestRetentionStayAggregateRow(
        Guid GuestId,
        Guid PropertyId,
        bool ProjectionSupported,
        bool HasOperationalStay,
        DateOnly? LatestDeparture,
        DateOnly? LatestNoShow,
        DateOnly? LatestCheckout);

    private sealed record GuestRetentionHoldRow(
        Guid GuestId,
        Guid PropertyId,
        DateTimeOffset PlacedAtUtc);
}
