namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestStayHistoryRepository(GuestsDbContext dbContext) : IGuestStayHistoryRepository
{
    public async Task ApplyAsync(GuestStayHistoryWriteModel stay, CancellationToken cancellationToken)
    {
        GuestStayHistoryEntry? current = await dbContext.StayHistory.FirstOrDefaultAsync(
            item => item.GuestId == stay.GuestId && item.ReservationId == stay.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            dbContext.StayHistory.Add(new GuestStayHistoryEntry(
                stay.ScopeId,
                stay.GuestId,
                stay.ReservationId,
                stay.PropertyId,
                stay.Role,
                stay.Arrival,
                stay.Departure,
                stay.Status,
                stay.CheckedInBusinessDate,
                stay.NoShowBusinessDate,
                stay.CheckedOutBusinessDate,
                stay.IsCurrentParticipant,
                stay.ReservationVersion));
            return;
        }

        current.Apply(
            stay.PropertyId,
            stay.Role,
            stay.Arrival,
            stay.Departure,
            stay.Status,
            stay.CheckedInBusinessDate,
            stay.NoShowBusinessDate,
            stay.CheckedOutBusinessDate,
            stay.IsCurrentParticipant,
            stay.ReservationVersion);
    }

    public async Task<IReadOnlyCollection<GuestStayHistoryItem>> ListAsync(
        Guid propertyId,
        Guid guestId,
        CancellationToken cancellationToken) =>
        await dbContext.StayHistory.AsNoTracking()
            .Where(stay => stay.PropertyId == propertyId && stay.GuestId == guestId)
            .OrderByDescending(stay => stay.Arrival)
            .ThenBy(stay => stay.ReservationId)
            .Select(stay => new GuestStayHistoryItem(
                stay.ReservationId,
                stay.PropertyId,
                stay.Role,
                stay.Arrival,
                stay.Departure,
                stay.Status,
                stay.CheckedInBusinessDate,
                stay.NoShowBusinessDate,
                stay.CheckedOutBusinessDate,
                stay.IsCurrentParticipant,
                stay.ReservationVersion))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
}
