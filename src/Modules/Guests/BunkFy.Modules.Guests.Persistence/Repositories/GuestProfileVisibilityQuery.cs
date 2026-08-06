namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;

internal static class GuestProfileVisibilityQuery
{
    internal static IQueryable<GuestProfile> VisibleGuestProfilesAt(
        this GuestsDbContext dbContext,
        Guid propertyId) => dbContext.GuestProfiles.Where(profile =>
        profile.Status != GuestProfileState.Anonymised &&
        (profile.OriginPropertyId == propertyId || dbContext.StayHistory.Any(stay =>
            stay.GuestId == profile.Id &&
            stay.PropertyId == propertyId &&
            stay.IsCurrentParticipant)) &&
        dbContext.ProcessingRestrictionProjections.Any(projection =>
            projection.PropertyId == propertyId &&
            projection.GuestId == profile.Id &&
            projection.ContractVersion == GuestProcessingRestrictionContract.CurrentVersion &&
            !projection.IsRestricted));
}
