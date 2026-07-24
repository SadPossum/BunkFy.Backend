namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.ProjectionRebuild;

internal sealed class DataRightsPropertiesProjectionRebuildWriter(
    IDataRightsPropertyProjectionRepository repository,
    DataRightsDbContext dbContext)
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
            return new ProjectionWriteResult(0, snapshots.Count);
        }

        foreach (PropertyTopologyProjectionExport property in snapshots)
        {
            await repository.ApplyTopologyAsync(
                new(
                    property.TenantId,
                    property.PropertyId,
                    property.Name,
                    property.Status,
                    property.Version),
                cancellationToken).ConfigureAwait(false);
            await repository.ApplyPolicyAsync(
                new(
                    property.TenantId,
                    property.PropertyId,
                    property.ProcessingStatus,
                    property.GovernancePolicy,
                    property.Version),
                cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ProjectionWriteResult(snapshots.Count);
    }
}
