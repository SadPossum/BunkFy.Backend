namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationGuestProfileProjectionRepository(ReservationsDbContext dbContext)
    : IReservationGuestProfileProjectionRepository
{
    public Task<bool> IsLinkableAsync(Guid propertyId, Guid guestId, CancellationToken cancellationToken) =>
        dbContext.GuestProfileProjections.AsNoTracking().AnyAsync(
            profile => profile.Id == guestId &&
                       profile.OriginPropertyId == propertyId &&
                       profile.Status == GuestStatus.Active &&
                       dbContext.GuestProcessingRestrictionProjections.Any(restriction =>
                           restriction.PropertyId == propertyId &&
                           restriction.GuestId == guestId &&
                           restriction.ContractVersion ==
                               GuestProcessingRestrictionContract.CurrentVersion &&
                           !restriction.IsRestricted),
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

    public async Task ApplyRestrictionAsync(
        ReservationGuestProcessingRestrictionProjectionWriteModel restriction,
        CancellationToken cancellationToken)
    {
        ReservationGuestProcessingRestrictionProjection? current =
            await dbContext.GuestProcessingRestrictionProjections.FirstOrDefaultAsync(
                item => item.PropertyId == restriction.PropertyId &&
                    item.GuestId == restriction.GuestId,
                cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            dbContext.GuestProcessingRestrictionProjections.Add(
                new ReservationGuestProcessingRestrictionProjection(
                    restriction.ScopeId,
                    restriction.PropertyId,
                    restriction.GuestId,
                    restriction.ContractVersion,
                    restriction.Revision,
                    restriction.IsRestricted));
            return;
        }

        current.Apply(
            restriction.ContractVersion,
            restriction.Revision,
            restriction.IsRestricted);
    }
}
