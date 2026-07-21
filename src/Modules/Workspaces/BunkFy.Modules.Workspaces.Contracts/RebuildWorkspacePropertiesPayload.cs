namespace BunkFy.Modules.Workspaces.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Workspaces' local Properties projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(WorkspacesModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildWorkspacePropertiesPayload(
    int ProjectionVersion = WorkspacesModuleMetadata.PropertiesProjectionVersion,
    int BatchSize = RebuildWorkspacePropertiesPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-workspace-properties";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
