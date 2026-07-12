namespace BunkFy.Modules.Guests.Persistence.Repositories;

using Gma.Framework.Pagination;
using BunkFy.Modules.Guests.Application.Mapping;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestProfileRepository(GuestsDbContext dbContext) : IGuestProfileRepository
{
    public Task AddAsync(GuestProfile profile, CancellationToken cancellationToken)
    {
        dbContext.GuestProfiles.Add(profile);
        return Task.CompletedTask;
    }

    public Task<GuestProfile?> GetVisibleAsync(
        Guid propertyId,
        Guid guestId,
        CancellationToken cancellationToken) => dbContext.GuestProfiles.FirstOrDefaultAsync(
        profile => profile.Id == guestId &&
                   (profile.OriginPropertyId == propertyId || dbContext.StayHistory.Any(stay =>
                       stay.GuestId == profile.Id && stay.PropertyId == propertyId && stay.IsCurrentParticipant)),
        cancellationToken);

    public async Task<GuestListResponse> ListVisibleAsync(
        Guid propertyId,
        string? search,
        GuestStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<GuestProfile> query = dbContext.GuestProfiles
            .AsNoTracking()
            .Where(profile => profile.OriginPropertyId == propertyId || dbContext.StayHistory.Any(stay =>
                stay.GuestId == profile.Id && stay.PropertyId == propertyId && stay.IsCurrentParticipant));
        if (status.HasValue)
        {
            GuestProfileState state = status.Value switch
            {
                GuestStatus.Active => GuestProfileState.Active,
                GuestStatus.Archived => GuestProfileState.Archived,
                _ => GuestProfileState.Unknown
            };
            query = query.Where(profile => profile.Status == state);
        }

        string? normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToUpperInvariant();
        if (normalizedSearch is not null)
        {
            query = query.Where(profile =>
                profile.DisplayNameSearch.Contains(normalizedSearch) ||
                (profile.LegalNameSearch != null && profile.LegalNameSearch.Contains(normalizedSearch)) ||
                (profile.EmailSearch != null && profile.EmailSearch.Contains(normalizedSearch)) ||
                (profile.PhoneSearch != null && profile.PhoneSearch.Contains(normalizedSearch)));
        }

        GuestProfile[] rows = await query
            .OrderBy(profile => profile.DisplayName)
            .ThenBy(profile => profile.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(rows.Select(profile => profile.ToDto()).ToArray(), pageRequest.Page, pageRequest.PageSize);
    }
}
