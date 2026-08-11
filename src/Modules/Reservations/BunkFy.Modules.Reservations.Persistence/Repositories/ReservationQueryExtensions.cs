namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal static class ReservationQueryExtensions
{
    public static IQueryable<Reservation> WithAggregateGraph(
        this IQueryable<Reservation> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query
            .Include(reservation => reservation.RequestedUnits)
            .Include(reservation => reservation.Guests)
            .AsSplitQuery();
    }

    public static IQueryable<Reservation> WithCreationReplayGraph(
        this IQueryable<Reservation> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Include(reservation => reservation.RequestedUnits);
    }
}
