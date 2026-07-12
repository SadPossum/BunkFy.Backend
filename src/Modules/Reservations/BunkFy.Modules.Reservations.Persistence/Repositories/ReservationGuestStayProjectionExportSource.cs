namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using System.Globalization;
using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Guests.Contracts;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class ReservationGuestStayProjectionExportSource(ReservationsDbContext dbContext)
    : IReservationGuestStayProjectionExportSource
{
    public async Task<ProjectionReadBatch<ReservationGuestStayProjectionExport>> ReadAsync(
        ProjectionRebuildRequest request,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        long? normalizedCursor = NormalizeCursor(cursor);
        IQueryable<Reservation> query = dbContext.Reservations.AsNoTracking();
        if (normalizedCursor.HasValue)
        {
            query = query.Where(reservation => reservation.ProjectionOrdinal > normalizedCursor.Value);
        }

        List<Reservation> rows = await query
            .Include(reservation => reservation.Guests)
            .Where(reservation => reservation.Guests.Any())
            .OrderBy(reservation => reservation.ProjectionOrdinal)
            .Take(request.BatchSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        bool hasMore = rows.Count > request.BatchSize;
        Reservation[] page = rows.Take(request.BatchSize).ToArray();
        ReservationGuestStayProjectionExport[] snapshots = page
            .SelectMany(reservation => reservation.Guests.Select(guest => new ReservationGuestStayProjectionExport(
                reservation.ScopeId,
                guest.GuestId,
                reservation.Id,
                reservation.PropertyId,
                (GuestStayRole)(int)guest.Role,
                guest.IsCurrent ? reservation.Arrival : guest.UnlinkedArrival!.Value,
                guest.IsCurrent ? reservation.Departure : guest.UnlinkedDeparture!.Value,
                MapStatus(guest.IsCurrent ? reservation.Status : guest.UnlinkedReservationStatus!.Value),
                guest.IsCurrent ? reservation.CheckedInBusinessDate : guest.UnlinkedCheckedInBusinessDate,
                guest.IsCurrent ? reservation.NoShowBusinessDate : guest.UnlinkedNoShowBusinessDate,
                guest.IsCurrent ? reservation.CheckedOutBusinessDate : guest.UnlinkedCheckedOutBusinessDate,
                guest.IsCurrent,
                guest.IsCurrent ? reservation.Version : guest.LinkVersion)))
            .ToArray();
        string? nextCursor = page.Length == 0
            ? null
            : page[^1].ProjectionOrdinal.ToString(CultureInfo.InvariantCulture);
        return new(snapshots, nextCursor, hasMore);
    }

    private static GuestStayStatus MapStatus(ReservationState status) => status switch
    {
        ReservationState.PendingAllocation => GuestStayStatus.PendingAllocation,
        ReservationState.Confirmed => GuestStayStatus.Confirmed,
        ReservationState.AllocationRejected => GuestStayStatus.AllocationRejected,
        ReservationState.CancellationPending => GuestStayStatus.CancellationPending,
        ReservationState.Cancelled => GuestStayStatus.Cancelled,
        ReservationState.CheckedIn => GuestStayStatus.CheckedIn,
        ReservationState.NoShowPending => GuestStayStatus.NoShowPending,
        ReservationState.NoShow => GuestStayStatus.NoShow,
        ReservationState.CheckoutPending => GuestStayStatus.CheckoutPending,
        ReservationState.CheckedOut => GuestStayStatus.CheckedOut,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static long? NormalizeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        return long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out long ordinal) && ordinal > 0
            ? ordinal
            : throw new ArgumentException("Projection export cursor must be a positive reservation ordinal.", nameof(cursor));
    }
}
