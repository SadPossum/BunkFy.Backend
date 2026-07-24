namespace BunkFy.Modules.Reservations.Application.Tasks;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;

internal sealed class RebuildReservationGuestRestrictionsTaskHandler(
    IGuestProcessingRestrictionProjectionExportSource source,
    IProjectionRebuildWriter<GuestProcessingRestrictionProjectionExport> writer,
    TaskProjectionRebuildRunner<GuestProcessingRestrictionProjectionExport> runner)
    : ITaskHandler<RebuildReservationGuestRestrictionsPayload>
{
    public Task HandleAsync(
        RebuildReservationGuestRestrictionsPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ProjectionRebuildRequest request = new(
            ReservationsModuleMetadata.GuestRestrictionsProjectionName,
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
