namespace BunkFy.Modules.Ingestion.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Poll an enabled external-source adapter connection once.")]
[TaskKind(ModuleTaskKind.Recurring)]
[TaskWorkerGroup(IngestionModuleMetadata.AdapterWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RunAdapterTaskPayload(Guid ConnectionId) : ITaskPayload
{
    public const string TaskName = "run-adapter";
    public const int PayloadVersion = 1;
}
