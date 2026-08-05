namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestStayHistoryRepository(
    GuestsDbContext dbContext,
    IGuestProcessingRestrictionProjectionRepository restrictionProjections,
    IGuestOperationLock operationLock)
    : IGuestStayHistoryRepository
{
    public async Task ApplyAsync(GuestStayHistoryWriteModel stay, CancellationToken cancellationToken)
    {
        await operationLock.AcquireGuestAsync(
            stay.ScopeId,
            stay.GuestId,
            cancellationToken).ConfigureAwait(false);

        if (stay.IsCurrentParticipant)
        {
            await restrictionProjections.EnsureAsync(
                stay.ScopeId,
                stay.PropertyId,
                stay.GuestId,
                stay.ObservedAtUtc,
                cancellationToken).ConfigureAwait(false);
        }

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
                stay.ReservationVersion,
                stay.ProjectionContractVersion));
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
            stay.ReservationVersion,
            stay.ProjectionContractVersion);
    }

    public async Task<GuestStayHistoryListResponse> ListAsync(
        Guid propertyId,
        Guid guestId,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        GuestStayHistoryItem[] rows = await dbContext.StayHistory.AsNoTracking()
            .Where(stay => stay.PropertyId == propertyId && stay.GuestId == guestId)
            .OrderByDescending(stay => stay.Arrival)
            .ThenBy(stay => stay.ReservationId)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize + 1)
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

        return new(
            rows.Take(pageRequest.PageSize).ToArray(),
            pageRequest.Page,
            pageRequest.PageSize,
            rows.Length > pageRequest.PageSize);
    }
}
