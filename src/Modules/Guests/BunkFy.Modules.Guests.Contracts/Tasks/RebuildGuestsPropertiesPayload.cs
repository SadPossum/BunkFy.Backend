namespace BunkFy.Modules.Guests.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Guests' local Properties projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(GuestsModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildGuestsPropertiesPayload(
    int ProjectionVersion = GuestsModuleMetadata.PropertiesProjectionVersion,
    int BatchSize = RebuildGuestsPropertiesPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-guests-properties";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
