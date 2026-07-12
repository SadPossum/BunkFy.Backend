namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Properties.Contracts;

internal sealed class GuestPropertyProjectionRepository(GuestsDbContext dbContext)
    : IGuestPropertyProjectionRepository
{
    public Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken) =>
        dbContext.PropertyProjections.AsNoTracking().AnyAsync(
            property => property.Id == propertyId && property.Status == PropertyStatus.Active,
            cancellationToken);

    public async Task ApplyAsync(
        GuestPropertyProjectionWriteModel property,
        CancellationToken cancellationToken)
    {
        GuestPropertyProjection? current = await dbContext.PropertyProjections.FirstOrDefaultAsync(
            item => item.Id == property.PropertyId,
            cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            dbContext.PropertyProjections.Add(new GuestPropertyProjection(
                property.ScopeId,
                property.PropertyId,
                property.Name,
                property.Status,
                property.Version));
            return;
        }

        current.Apply(property.Name, property.Status, property.Version);
    }
}
