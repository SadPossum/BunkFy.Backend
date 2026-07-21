namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using Gma.Framework.ProjectionRebuild;

internal sealed class WorkspacePropertiesProjectionRebuildWriter(
    IWorkspacePropertyProjectionRepository repository,
    WorkspacesDbContext dbContext)
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
            await repository.ApplyAsync(
                new(property.TenantId, property.PropertyId, property.Name,
                    property.Status, property.Version),
                cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ProjectionWriteResult(snapshots.Count);
    }
}
