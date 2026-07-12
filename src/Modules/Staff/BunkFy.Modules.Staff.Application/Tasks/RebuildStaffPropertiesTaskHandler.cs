namespace BunkFy.Modules.Staff.Application.Tasks;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Contracts;

internal sealed class RebuildStaffPropertiesTaskHandler(
    IPropertiesTopologyProjectionExportSource source,
    IProjectionRebuildWriter<PropertyTopologyProjectionExport> writer,
    TaskProjectionRebuildRunner<PropertyTopologyProjectionExport> runner)
    : ITaskHandler<RebuildStaffPropertiesPayload>
{
    public Task HandleAsync(RebuildStaffPropertiesPayload payload, TaskExecutionContext context,
        CancellationToken cancellationToken) => runner.RunAsync(StaffModuleMetadata.Name,
        new ProjectionRebuildRequest(StaffModuleMetadata.PropertiesProjectionName,
            payload.ProjectionVersion, payload.BatchSize, payload.DryRun, payload.Cursor),
        source, writer, context, scopeAware: true, cancellationToken);
}
