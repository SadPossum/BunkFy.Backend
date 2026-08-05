namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Verify durable tenant destruction proofs and seal the terminal receipt.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(DataRightsModuleMetadata.TenantTerminationWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record VerifyTenantTerminationPayload(
    Guid ProcessId,
    long OperationRevision) : ITaskPayload
{
    public const string TaskName = "verify-tenant-termination";
    public const int PayloadVersion = 1;
}
