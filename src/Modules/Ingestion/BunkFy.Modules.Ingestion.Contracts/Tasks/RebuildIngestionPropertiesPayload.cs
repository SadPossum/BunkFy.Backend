namespace BunkFy.Modules.Ingestion.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Ingestion's local active-property projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(IngestionModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildIngestionPropertiesPayload(
    int ProjectionVersion = IngestionModuleMetadata.PropertyProjectionVersion,
    int BatchSize = RebuildIngestionPropertiesPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-ingestion-properties";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
