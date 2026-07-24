namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild DataRights' local Properties governance projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildDataRightsPropertiesPayload(
    int ProjectionVersion = DataRightsModuleMetadata.PropertiesProjectionVersion,
    int BatchSize = RebuildDataRightsPropertiesPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-data-rights-properties";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
