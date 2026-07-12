namespace BunkFy.Modules.Guests.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Guests' local reservation stay history projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(GuestsModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildGuestStayHistoryPayload(
    int ProjectionVersion = GuestsModuleMetadata.StayHistoryProjectionVersion,
    int BatchSize = RebuildGuestStayHistoryPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-guest-stay-history";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
