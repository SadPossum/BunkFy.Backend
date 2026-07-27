namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Delete one expired protected DataRights export artifact.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.ExportWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record DeleteExpiredDataRightsExportArtifactPayload(
    Guid ArtifactId,
    Guid CaseId,
    DataRightsCaseType CaseType,
    Guid? PropertyId,
    long DecisionRevision,
    DateTimeOffset ExpiresAtUtc) : ITaskPayload
{
    public const string TaskName = "delete-expired-data-rights-export";
    public const int PayloadVersion = 1;
}
