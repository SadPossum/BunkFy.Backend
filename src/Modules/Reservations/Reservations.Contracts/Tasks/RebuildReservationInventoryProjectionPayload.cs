namespace Reservations.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Reservations' local Inventory availability projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(ReservationsModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildReservationInventoryProjectionPayload(
    int ProjectionVersion = ReservationsModuleMetadata.InventoryProjectionVersion,
    int BatchSize = RebuildReservationInventoryProjectionPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-reservation-inventory-projection";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
