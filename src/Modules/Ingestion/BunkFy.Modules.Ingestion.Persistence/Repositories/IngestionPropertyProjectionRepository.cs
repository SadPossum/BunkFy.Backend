namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionPropertyProjectionRepository(IngestionDbContext dbContext)
    : IIngestionPropertyProjectionRepository, IRetentionFenceRepository
{
    public async Task ApplyAsync(
        IngestionPropertyProjectionWriteModel property,
        CancellationToken cancellationToken)
    {
        IngestionPropertyProjection? projection = dbContext.PropertyProjections.Local.FirstOrDefault(
            item => item.Id == property.PropertyId && item.ScopeId == property.ScopeId) ??
            await dbContext.PropertyProjections.FirstOrDefaultAsync(
                item => item.Id == property.PropertyId,
                cancellationToken).ConfigureAwait(false);
        if (projection is null)
        {
            projection = IngestionPropertyProjection.Create(property.PropertyId, property.ScopeId);
            dbContext.PropertyProjections.Add(projection);
        }

        projection.Apply(
            property.Name,
            property.Code,
            property.IsActive,
            property.SourceVersion);
    }

    public Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken) =>
        dbContext.PropertyProjections.AsNoTracking().AnyAsync(
            property => property.Id == propertyId && property.IsKnown && property.IsActive,
            cancellationToken);

    public async Task<bool> TryAdvanceAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        IngestionPropertyProjection? property = dbContext.PropertyProjections.Local.FirstOrDefault(
            item => item.Id == propertyId) ?? await dbContext.PropertyProjections.FirstOrDefaultAsync(
            item => item.Id == propertyId && item.IsKnown,
            cancellationToken).ConfigureAwait(false);
        if (property is null || !property.IsKnown)
        {
            return false;
        }

        property.AdvanceRetentionFence();
        return true;
    }
}
