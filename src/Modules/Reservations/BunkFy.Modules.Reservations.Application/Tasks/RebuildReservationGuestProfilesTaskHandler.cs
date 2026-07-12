namespace BunkFy.Modules.Reservations.Application.Tasks;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class RebuildReservationGuestProfilesTaskHandler(
    IGuestProfileEligibilityProjectionExportSource source,
    IProjectionRebuildWriter<GuestProfileEligibilityProjectionExport> writer,
    TaskProjectionRebuildRunner<GuestProfileEligibilityProjectionExport> runner)
    : ITaskHandler<RebuildReservationGuestProfilesPayload>
{
    public Task HandleAsync(
        RebuildReservationGuestProfilesPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ProjectionRebuildRequest request = new(
            ReservationsModuleMetadata.GuestProfilesProjectionName,
            payload.ProjectionVersion,
            payload.BatchSize,
            payload.DryRun,
            payload.Cursor);
        return runner.RunAsync(
            ReservationsModuleMetadata.Name,
            request,
            source,
            writer,
            context,
            scopeAware: true,
            cancellationToken);
    }
}
