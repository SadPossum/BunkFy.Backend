namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Execute one scoped approved DataRights anonymisation owner work item.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.AnonymisationWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record ExecuteDataRightsAnonymisationPayloadV2(
    Guid WorkItemId,
    Guid CaseId,
    DataRightsCaseType CaseType,
    DataRightsExecutionScopeKind ScopeKind,
    Guid? PropertyId,
    long ApprovalRevision,
    long ExecutionRevision) : ITaskPayload
{
    public const string TaskName = ExecuteDataRightsAnonymisationPayload.TaskName;
    public const int PayloadVersion = 2;
}
