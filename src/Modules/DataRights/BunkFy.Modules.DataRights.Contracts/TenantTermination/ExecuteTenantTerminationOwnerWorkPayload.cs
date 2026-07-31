namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Execute one scoped tenant-termination owner work item.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.TenantTerminationWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record ExecuteTenantTerminationOwnerWorkPayload(
    Guid ProcessId,
    Guid WorkItemId,
    long OperationRevision,
    TenantTerminationContributionPhase Phase,
    string OwnerKey) : ITaskPayload
{
    public const string TaskName =
        "execute-tenant-termination-owner-work";
    public const int PayloadVersion = 1;
}
