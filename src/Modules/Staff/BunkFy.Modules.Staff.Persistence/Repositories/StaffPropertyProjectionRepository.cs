namespace BunkFy.Modules.Staff.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Application.Ports;

internal sealed class StaffPropertyProjectionRepository(StaffDbContext dbContext)
    : IStaffPropertyProjectionRepository
{
    public Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken) =>
        dbContext.PropertyProjections.AsNoTracking().AnyAsync(property =>
            property.Id == propertyId && property.Status == PropertyStatus.Active, cancellationToken);

    public async Task<bool> AreAllActiveAsync(
        IReadOnlyCollection<Guid> propertyIds,
        CancellationToken cancellationToken)
    {
        Guid[] distinctIds = propertyIds.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return true;
        }

        int activeCount = await dbContext.PropertyProjections.AsNoTracking()
            .CountAsync(
                property => distinctIds.Contains(property.Id) &&
                    property.Status == PropertyStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);
        return activeCount == distinctIds.Length;
    }

    public async Task ApplyAsync(StaffPropertyProjectionWriteModel property,
        CancellationToken cancellationToken)
    {
        StaffPropertyProjection? current = await dbContext.PropertyProjections.FirstOrDefaultAsync(
            item => item.Id == property.PropertyId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            dbContext.PropertyProjections.Add(new StaffPropertyProjection(property.ScopeId,
                property.PropertyId, property.Name, property.Status, property.Version));
            return;
        }

        current.Apply(property.Name, property.Status, property.Version);
    }
}
