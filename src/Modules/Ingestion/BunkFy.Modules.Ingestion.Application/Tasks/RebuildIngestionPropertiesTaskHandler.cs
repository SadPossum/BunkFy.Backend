namespace BunkFy.Modules.Ingestion.Application.Tasks;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Properties.Contracts;

internal sealed class RebuildIngestionPropertiesTaskHandler(
    IPropertiesTopologyProjectionExportSource source,
    IProjectionRebuildWriter<PropertyTopologyProjectionExport> writer,
    TaskProjectionRebuildRunner<PropertyTopologyProjectionExport> runner)
    : ITaskHandler<RebuildIngestionPropertiesPayload>
{
    public Task HandleAsync(
        RebuildIngestionPropertiesPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken) => runner.RunAsync(
        IngestionModuleMetadata.Name,
        new ProjectionRebuildRequest(
            IngestionModuleMetadata.PropertyProjectionName,
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
