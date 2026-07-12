namespace BunkFy.Modules.Ingestion.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Run a pinned parser against one retained rejected observation.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(IngestionModuleMetadata.MaintenanceWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record ReprocessObservationPayload(
    Guid AttemptId,
    string ParserType,
    int ParserVersion,
    int MaxAttempts) : ITaskPayload
{
    public const string TaskName = "reprocess-observation";
    public const int PayloadVersion = 1;
    public const int MaximumAttempts = 10;
}
