namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Properties.Contracts;

internal sealed class IngestionPropertyProjectionRebuildWriter(
    IIngestionPropertyProjectionRepository properties,
    IngestionDbContext dbContext)
    : IProjectionRebuildWriter<PropertyTopologyProjectionExport>
{
    public async Task<ProjectionWriteResult> WriteAsync(
        ProjectionRebuildRequest request,
        IReadOnlyCollection<PropertyTopologyProjectionExport> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshots);
        if (request.DryRun)
        {
            return new(writtenCount: 0, skippedCount: snapshots.Count);
        }

        foreach (PropertyTopologyProjectionExport property in snapshots)
        {
            await properties.ApplyAsync(new(
                property.TenantId,
                property.PropertyId,
                property.Name,
                property.Code,
                property.Status == PropertyStatus.Active,
                property.Version), cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(snapshots.Count);
    }
}
