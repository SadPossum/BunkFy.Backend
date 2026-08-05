namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Generate one protected tenant-termination export fragment.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.TenantTerminationWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record ExecuteTenantTerminationExportOwnerWorkPayload(
    Guid ProcessId,
    Guid WorkItemId,
    long OperationRevision,
    string OwnerKey) : ITaskPayload
{
    public const string TaskName =
        "execute-tenant-termination-export-owner-work";
    public const int PayloadVersion = 1;
}
