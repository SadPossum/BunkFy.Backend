namespace BunkFy.Modules.Workspaces.Application.Tasks;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;

internal sealed class RebuildWorkspacePropertiesTaskHandler(
    IPropertiesTopologyProjectionExportSource source,
    IProjectionRebuildWriter<PropertyTopologyProjectionExport> writer,
    TaskProjectionRebuildRunner<PropertyTopologyProjectionExport> runner)
    : ITaskHandler<RebuildWorkspacePropertiesPayload>
{
    public Task HandleAsync(
        RebuildWorkspacePropertiesPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken) => runner.RunAsync(
        WorkspacesModuleMetadata.Name,
        new ProjectionRebuildRequest(
            WorkspacesModuleMetadata.PropertiesProjectionName,
            payload.ProjectionVersion,
            payload.BatchSize,
            payload.DryRun,
            payload.Cursor),
        source,
        writer,
        context,
        scopeAware: true,
        cancellationToken);
}
