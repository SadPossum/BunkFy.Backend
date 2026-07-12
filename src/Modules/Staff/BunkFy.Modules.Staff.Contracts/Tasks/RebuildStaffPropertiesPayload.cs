namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Staff's local Properties projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(StaffModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildStaffPropertiesPayload(
    int ProjectionVersion = StaffModuleMetadata.PropertiesProjectionVersion,
    int BatchSize = RebuildStaffPropertiesPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-staff-properties";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
