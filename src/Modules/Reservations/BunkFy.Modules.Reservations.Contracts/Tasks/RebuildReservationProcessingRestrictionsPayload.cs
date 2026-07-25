namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Reservations' processing restriction state.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(ReservationsModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildReservationProcessingRestrictionsPayload(
    int ProjectionVersion =
        ReservationsModuleMetadata.ProcessingRestrictionProjectionVersion,
    int BatchSize =
        RebuildReservationProcessingRestrictionsPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-reservation-processing-restrictions";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
