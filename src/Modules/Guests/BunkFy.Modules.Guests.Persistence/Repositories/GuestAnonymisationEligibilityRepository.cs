namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.Models;
using BunkFy.Modules.Properties.Contracts;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestAnonymisationEligibilityRepository(GuestsDbContext dbContext)
    : IGuestAnonymisationEligibilityRepository
{
    public async Task<GuestAnonymisationEligibilitySnapshot?> LoadAsync(
        Guid guestId,
        CancellationToken cancellationToken)
    {
        GuestProfile? profile = await dbContext.GuestProfiles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == guestId, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        GuestAnonymisationStaySnapshot[] stays = await dbContext.StayHistory
            .AsNoTracking()
            .Where(stay => stay.GuestId == guestId)
            .OrderBy(stay => stay.PropertyId)
            .ThenBy(stay => stay.ReservationId)
            .Select(stay => new GuestAnonymisationStaySnapshot(
                stay.PropertyId,
                stay.ReservationId,
                stay.Role,
                stay.Status,
                stay.IsCurrentParticipant,
                stay.ReservationVersion,
                stay.ProjectionContractVersion))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] activeHoldProperties = await dbContext.DataHolds.AsNoTracking()
            .Where(hold =>
                hold.GuestId == guestId &&
                hold.State == GuestDataHoldState.Active)
            .Select(hold => hold.PropertyId)
            .Distinct()
            .OrderBy(propertyId => propertyId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid[] propertyIds = stays.Select(stay => stay.PropertyId)
            .Append(profile.OriginPropertyId)
            .Concat(activeHoldProperties)
            .Distinct()
            .OrderBy(propertyId => propertyId)
            .ToArray();
        GuestPropertyProjection[] propertyRows =
            propertyIds.Length > GuestAnonymisationEligibilityContract.MaximumAffectedProperties
                ? []
                : await dbContext.PropertyProjections.AsNoTracking()
                    .Include(property => property.GovernancePolicy)
                    .ThenInclude(policy => policy!.Acknowledgements)
                    .Where(property => propertyIds.Contains(property.Id))
                    .OrderBy(property => property.Id)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
        GuestAnonymisationPropertySnapshot[] properties = propertyRows
            .Select(property => new GuestAnonymisationPropertySnapshot(
                property.Id,
                property.IsKnown,
                property.Status == PropertyStatus.Active,
                property.ProcessingStatus,
                property.TopologySourceVersion,
                property.PolicySourceVersion,
                property.GovernancePolicy.ToContract()))
            .ToArray();

        return new(
            profile.Version,
            profile.Status,
            profile.OriginPropertyId,
            stays,
            activeHoldProperties,
            properties);
    }
}
