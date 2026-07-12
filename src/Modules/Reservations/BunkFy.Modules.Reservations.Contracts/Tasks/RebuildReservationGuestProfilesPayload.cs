namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Scoping;
using Gma.Framework.Tasks;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Rebuild Reservations' local Guest profile eligibility projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(ReservationsModuleMetadata.ProjectionWorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildReservationGuestProfilesPayload(
    int ProjectionVersion = ReservationsModuleMetadata.GuestProfilesProjectionVersion,
    int BatchSize = RebuildReservationGuestProfilesPayload.DefaultBatchSize,
    bool DryRun = false,
    string? Cursor = null) : ITaskPayload
{
    public const string TaskName = "rebuild-reservation-guest-profiles";
    public const int PayloadVersion = 1;
    public const int DefaultBatchSize = 100;
}
