namespace Reservations.Application.Tasks;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;
using Inventory.Contracts;
using Reservations.Contracts;

internal sealed class RebuildReservationInventoryProjectionTaskHandler(
    IInventoryAvailabilityProjectionExportSource source,
    IProjectionRebuildWriter<InventoryAvailabilityProjectionExport> writer,
    TaskProjectionRebuildRunner<InventoryAvailabilityProjectionExport> runner)
    : ITaskHandler<RebuildReservationInventoryProjectionPayload>
{
    public Task HandleAsync(
        RebuildReservationInventoryProjectionPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ProjectionRebuildRequest request = new(
            ReservationsModuleMetadata.InventoryProjectionName,
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
