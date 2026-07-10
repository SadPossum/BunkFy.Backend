namespace Inventory.Contracts;

using Gma.Framework.Tasks;
using Gma.Framework.Scoping;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Inventory's local Properties topology projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(InventoryModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildInventoryTopologyPayload(
    int ProjectionVersion = InventoryModuleMetadata.TopologyProjectionVersion,
    int BatchSize = RebuildInventoryTopologyPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-inventory-topology";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
