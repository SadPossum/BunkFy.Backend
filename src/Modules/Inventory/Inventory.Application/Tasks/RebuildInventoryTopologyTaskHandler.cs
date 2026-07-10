namespace Inventory.Application.Tasks;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using Inventory.Contracts;
using Properties.Contracts;

internal sealed class RebuildInventoryTopologyTaskHandler(
    IPropertiesTopologyProjectionExportSource source,
    IProjectionRebuildWriter<PropertyTopologyProjectionExport> writer,
    TaskProjectionRebuildRunner<PropertyTopologyProjectionExport> runner)
    : ITaskHandler<RebuildInventoryTopologyPayload>
{
    public Task HandleAsync(
        RebuildInventoryTopologyPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ProjectionRebuildRequest request = new(
            InventoryModuleMetadata.TopologyProjectionName,
            payload.ProjectionVersion,
            payload.BatchSize,
            payload.DryRun,
            payload.Cursor);

        return runner.RunAsync(
            InventoryModuleMetadata.Name,
            request,
            source,
            writer,
            context,
            scopeAware: true,
            cancellationToken);
    }
}
