namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using System.Globalization;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.ProjectionRebuild;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationProcessingRestrictionProjectionRebuildSource(
    ReservationsDbContext dbContext)
    : IReservationProcessingRestrictionProjectionRebuildSource
{
    public async Task<
        ProjectionReadBatch<ReservationProcessingRestrictionProjectionSnapshot>>
        ReadAsync(
            ProjectionRebuildRequest request,
            string? cursor,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        long? normalizedCursor = NormalizeCursor(cursor);
        IQueryable<Reservation> query = dbContext.Reservations.AsNoTracking();
        if (normalizedCursor.HasValue)
        {
            query = query.Where(
                reservation =>
                    reservation.ProjectionOrdinal > normalizedCursor.Value);
        }

        List<ReservationRebuildRow> rows = await query
            .OrderBy(reservation => reservation.ProjectionOrdinal)
            .Select(reservation => new ReservationRebuildRow(
                reservation.ScopeId,
                reservation.PropertyId,
                reservation.Id,
                reservation.ProjectionOrdinal,
                reservation.CreatedAtUtc))
            .Take(request.BatchSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        bool hasMore = rows.Count > request.BatchSize;
        ReservationRebuildRow[] page = rows.Take(request.BatchSize).ToArray();
        if (page.Length == 0)
        {
            return new([], null, hasMore: false);
        }

        Guid[] reservationIds = page
            .Select(reservation => reservation.ReservationId)
            .ToArray();
        ReservationProcessingRestriction[] restrictions =
            await dbContext.ProcessingRestrictions
                .AsNoTracking()
                .Where(restriction =>
                    reservationIds.Contains(restriction.ReservationId))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        ILookup<Guid, ReservationProcessingRestriction> byReservation =
            restrictions.ToLookup(restriction => restriction.ReservationId);

        ReservationProcessingRestrictionProjectionSnapshot[] snapshots = page
            .Select(reservation =>
            {
                ReservationProcessingRestriction[] source = byReservation[
                    reservation.ReservationId].ToArray();
                return new ReservationProcessingRestrictionProjectionSnapshot(
                    reservation.ScopeId,
                    reservation.PropertyId,
                    reservation.ReservationId,
                    ReservationProcessingRestrictionContract.CurrentVersion,
                    source.Sum(restriction => restriction.Version),
                    source.Count(restriction =>
                        restriction.Status ==
                            ReservationProcessingRestrictionStatus.Active),
                    source.Length == 0
                        ? reservation.CreatedAtUtc
                        : source.Max(restriction =>
                            restriction.ReleasedAtUtc ??
                            restriction.AppliedAtUtc));
            })
            .ToArray();
        string nextCursor = page[^1].ProjectionOrdinal.ToString(
            CultureInfo.InvariantCulture);
        return new(snapshots, nextCursor, hasMore);
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
                "Projection rebuild cursor must be a positive reservation ordinal.",
                nameof(cursor));
    }

    private sealed record ReservationRebuildRow(
        string ScopeId,
        Guid PropertyId,
        Guid ReservationId,
        long ProjectionOrdinal,
        DateTimeOffset CreatedAtUtc);
}
