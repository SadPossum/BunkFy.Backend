namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Reservations' local Properties projection used for property-local scheduling.")]
[TaskKind(ModuleTaskKind.Recurring)]
[TaskWorkerGroup(ReservationsModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildReservationPropertiesPayload(
    int ProjectionVersion = ReservationsModuleMetadata.PropertyProjectionVersion,
    int BatchSize = RebuildReservationPropertiesPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-reservation-properties-projection";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
