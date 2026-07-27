namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Generate one approved protected DataRights access export.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.ExportWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record GenerateDataRightsExportPayload(
    Guid ArtifactId,
    Guid CaseId,
    DataRightsCaseType CaseType,
    Guid? PropertyId,
    long DecisionRevision) : ITaskPayload
{
    public const string TaskName = "generate-data-rights-export";
    public const int PayloadVersion = 1;
}
