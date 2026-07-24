namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Reservations' local Guest processing restriction projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(ReservationsModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildReservationGuestRestrictionsPayload(
    int ProjectionVersion = ReservationsModuleMetadata.GuestRestrictionsProjectionVersion,
    int BatchSize = RebuildReservationGuestRestrictionsPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-reservation-guest-restrictions";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
