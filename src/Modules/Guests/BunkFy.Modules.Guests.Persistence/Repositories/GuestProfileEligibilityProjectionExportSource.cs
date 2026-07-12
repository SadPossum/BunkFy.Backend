namespace BunkFy.Modules.Guests.Persistence.Repositories;

using System.Globalization;
using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestProfileEligibilityProjectionExportSource(GuestsDbContext dbContext)
    : IGuestProfileEligibilityProjectionExportSource
{
    public async Task<ProjectionReadBatch<GuestProfileEligibilityProjectionExport>> ReadAsync(
        ProjectionRebuildRequest request,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        long? normalizedCursor = NormalizeCursor(cursor);
        IQueryable<GuestProfile> query = dbContext.GuestProfiles.AsNoTracking();
        if (normalizedCursor.HasValue)
        {
            query = query.Where(profile => profile.ProjectionOrdinal > normalizedCursor.Value);
        }

        List<GuestProfile> rows = await query
            .OrderBy(profile => profile.ProjectionOrdinal)
            .Take(request.BatchSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        bool hasMore = rows.Count > request.BatchSize;
        GuestProfile[] page = rows.Take(request.BatchSize).ToArray();
        string? nextCursor = page.Length == 0
            ? null
            : page[^1].ProjectionOrdinal.ToString(CultureInfo.InvariantCulture);
        return new(
            page.Select(profile => new GuestProfileEligibilityProjectionExport(
                profile.ScopeId,
                profile.Id,
                profile.OriginPropertyId,
                profile.Status == GuestProfileState.Active ? GuestStatus.Active : GuestStatus.Archived,
                profile.Version)).ToArray(),
            nextCursor,
            hasMore);
    }

    private static long? NormalizeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        return long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out long ordinal) && ordinal > 0
            ? ordinal
            : throw new ArgumentException("Projection export cursor must be a positive guest ordinal.", nameof(cursor));
    }
}
