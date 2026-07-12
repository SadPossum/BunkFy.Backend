namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Guests.Contracts;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Application.Ports;

internal sealed class ReservationGuestProfileProjectionRepository(ReservationsDbContext dbContext)
    : IReservationGuestProfileProjectionRepository
{
    public Task<bool> IsLinkableAsync(Guid propertyId, Guid guestId, CancellationToken cancellationToken) =>
        dbContext.GuestProfileProjections.AsNoTracking().AnyAsync(
            profile => profile.Id == guestId &&
                       profile.OriginPropertyId == propertyId &&
                       profile.Status == GuestStatus.Active,
            cancellationToken);

    public async Task ApplyAsync(
        ReservationGuestProfileProjectionWriteModel profile,
        CancellationToken cancellationToken)
    {
        ReservationGuestProfileProjection? current = await dbContext.GuestProfileProjections.FirstOrDefaultAsync(
            item => item.Id == profile.GuestId,
            cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            dbContext.GuestProfileProjections.Add(new ReservationGuestProfileProjection(
                profile.ScopeId,
                profile.GuestId,
                profile.OriginPropertyId,
                profile.Status,
                profile.Version));
            return;
        }

        current.Apply(profile.OriginPropertyId, profile.Status, profile.Version);
    }
}
