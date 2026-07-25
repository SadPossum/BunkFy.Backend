namespace BunkFy.Modules.Reservations.Application.Tasks;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;

internal sealed class RebuildReservationProcessingRestrictionsTaskHandler(
    IReservationProcessingRestrictionProjectionRebuildSource source,
    IProjectionRebuildWriter<ReservationProcessingRestrictionProjectionSnapshot>
        writer,
    TaskProjectionRebuildRunner<
        ReservationProcessingRestrictionProjectionSnapshot> runner)
    : ITaskHandler<RebuildReservationProcessingRestrictionsPayload>
{
    public Task HandleAsync(
        RebuildReservationProcessingRestrictionsPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ProjectionRebuildRequest request = new(
            ReservationsModuleMetadata.ProcessingRestrictionProjectionName,
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
