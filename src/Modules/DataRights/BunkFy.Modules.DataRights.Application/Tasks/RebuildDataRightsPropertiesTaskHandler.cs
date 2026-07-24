namespace BunkFy.Modules.DataRights.Application.Tasks;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;

internal sealed class RebuildDataRightsPropertiesTaskHandler(
    IPropertiesTopologyProjectionExportSource source,
    IProjectionRebuildWriter<PropertyTopologyProjectionExport> writer,
    TaskProjectionRebuildRunner<PropertyTopologyProjectionExport> runner)
    : ITaskHandler<RebuildDataRightsPropertiesPayload>
{
    public Task HandleAsync(
        RebuildDataRightsPropertiesPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken) =>
        runner.RunAsync(
            DataRightsModuleMetadata.Name,
            new ProjectionRebuildRequest(
                DataRightsModuleMetadata.PropertiesProjectionName,
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
