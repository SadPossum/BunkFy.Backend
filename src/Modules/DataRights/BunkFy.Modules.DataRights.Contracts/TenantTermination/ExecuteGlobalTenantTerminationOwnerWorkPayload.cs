namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Execute one tenant-termination owner work item outside the tenant task scope.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.TenantTerminationWorkerGroup)]
[SupportsTaskControl]
public sealed record ExecuteGlobalTenantTerminationOwnerWorkPayload(
    string TenantId,
    Guid ProcessId,
    Guid WorkItemId,
    long OperationRevision,
    TenantTerminationContributionPhase Phase,
    string OwnerKey) : ITaskPayload
{
    public const string TaskName =
        "execute-global-tenant-termination-owner-work";
    public const int PayloadVersion = 1;
}
