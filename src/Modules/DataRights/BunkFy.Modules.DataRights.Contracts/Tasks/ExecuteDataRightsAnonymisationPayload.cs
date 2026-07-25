namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Execute one approved DataRights anonymisation owner work item.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.AnonymisationWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record ExecuteDataRightsAnonymisationPayload(
    Guid WorkItemId,
    Guid CaseId,
    Guid PropertyId,
    long ApprovalRevision,
    long ExecutionRevision) : ITaskPayload
{
    public const string TaskName = "execute-data-rights-anonymisation";
    public const int PayloadVersion = 1;
}
