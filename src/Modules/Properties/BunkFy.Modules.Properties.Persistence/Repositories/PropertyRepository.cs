namespace BunkFy.Modules.Properties.Persistence.Repositories;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

internal sealed class PropertyRepository(PropertiesDbContext dbContext) : IPropertyRepository
{
    public async Task AddAsync(Property property, CancellationToken cancellationToken)
    {
        await dbContext.Properties.AddAsync(property, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Property?> GetAsync(Guid propertyId, CancellationToken cancellationToken) =>
        await dbContext.Properties
            .FirstOrDefaultAsync(property => property.Id == propertyId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> CodeExistsAsync(string code, Guid? excludingPropertyId, CancellationToken cancellationToken)
    {
        PropertyCode normalized = PropertyCode.Create(code).Value;
        return await dbContext.Properties
            .AnyAsync(
                property => property.Code == normalized &&
                            (excludingPropertyId == null || property.Id != excludingPropertyId.Value),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
