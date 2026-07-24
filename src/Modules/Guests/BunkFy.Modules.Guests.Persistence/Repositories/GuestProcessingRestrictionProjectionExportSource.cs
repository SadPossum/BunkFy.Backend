namespace BunkFy.Modules.Guests.Persistence.Repositories;

using System.Globalization;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using Gma.Framework.ProjectionRebuild;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestProcessingRestrictionProjectionExportSource(
    GuestsDbContext dbContext)
    : IGuestProcessingRestrictionProjectionExportSource
{
    public async Task<ProjectionReadBatch<GuestProcessingRestrictionProjectionExport>> ReadAsync(
        ProjectionRebuildRequest request,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        long? normalizedCursor = NormalizeCursor(cursor);
        IQueryable<GuestProcessingRestrictionProjection> query =
            dbContext.ProcessingRestrictionProjections.AsNoTracking();
        if (normalizedCursor.HasValue)
        {
            query = query.Where(projection =>
                projection.ProjectionOrdinal > normalizedCursor.Value);
        }

        List<GuestProcessingRestrictionProjection> rows = await query
            .OrderBy(projection => projection.ProjectionOrdinal)
            .Take(request.BatchSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        bool hasMore = rows.Count > request.BatchSize;
        GuestProcessingRestrictionProjection[] page = rows
            .Take(request.BatchSize)
            .ToArray();
        string? nextCursor = page.Length == 0
            ? null
            : page[^1].ProjectionOrdinal.ToString(CultureInfo.InvariantCulture);
        return new(
            page.Select(projection => new GuestProcessingRestrictionProjectionExport(
                projection.ScopeId,
                projection.PropertyId,
                projection.GuestId,
                projection.ContractVersion,
                projection.Revision,
                projection.IsRestricted)).ToArray(),
            nextCursor,
            hasMore);
    }

    private static long? NormalizeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        return long.TryParse(
            cursor,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out long ordinal) && ordinal > 0
            ? ordinal
            : throw new ArgumentException(
                "Projection export cursor must be a positive restriction-state ordinal.",
                nameof(cursor));
    }
}
