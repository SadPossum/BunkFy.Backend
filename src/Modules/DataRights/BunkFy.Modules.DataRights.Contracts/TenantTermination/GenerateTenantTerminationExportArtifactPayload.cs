namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Bind protected tenant-termination fragments into the final export artifact.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.TenantTerminationWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record GenerateTenantTerminationExportArtifactPayload(
    Guid ProcessId,
    long OperationRevision) : ITaskPayload
{
    public const string TaskName =
        "generate-tenant-termination-export-artifact";
    public const int PayloadVersion = 1;
}
