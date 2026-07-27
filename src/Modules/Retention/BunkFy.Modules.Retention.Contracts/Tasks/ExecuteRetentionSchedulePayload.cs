namespace BunkFy.Modules.Retention.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Execute one versioned owner-module retention schedule.")]
[TaskKind(ModuleTaskKind.Recurring)]
[TaskWorkerGroup(RetentionModuleMetadata.WorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record ExecuteRetentionSchedulePayload(
    string OwnerKey,
    string DataClassKey,
    int ExecutionPolicyVersion,
    RetentionTargetScopeKind TargetScopeKind,
    Guid? PropertyId = null) : ITaskPayload
{
    public const string TaskName = "execute-retention-schedule";
    public const int PayloadVersion = 1;
}
