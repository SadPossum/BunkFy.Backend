namespace Reservations.Persistence.Repositories;

using Gma.Framework.ProjectionRebuild;
using Inventory.Contracts;
using Reservations.Application.Ports;

internal sealed class InventoryProjectionRebuildWriter(
    IInventoryProjectionRepository repository,
    ReservationsDbContext dbContext)
    : IProjectionRebuildWriter<InventoryAvailabilityProjectionExport>
{
    public async Task<ProjectionWriteResult> WriteAsync(
        ProjectionRebuildRequest request,
        IReadOnlyCollection<InventoryAvailabilityProjectionExport> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshots);

        if (request.DryRun)
        {
            return new ProjectionWriteResult(0, snapshots.Count);
        }

        foreach (InventoryAvailabilityProjectionExport snapshot in snapshots)
        {
            foreach (InventoryUnitProjectionExport unit in snapshot.Units)
            {
                await repository.ApplyUnitAsync(
                    new(
                        snapshot.TenantId,
                        unit.InventoryUnitId,
                        snapshot.PropertyId,
                        unit.RoomId,
                        unit.BedId,
                        unit.Kind,
                        unit.Label,
                        unit.IsTopologyActive,
                        unit.IsSellable,
                        unit.ConfigurationVersion,
                        unit.UnitVersion),
                    cancellationToken).ConfigureAwait(false);

                foreach (ManualInventoryBlockProjectionExport block in unit.Blocks)
                {
                    await repository.ApplyBlockAsync(
                        new(
                            snapshot.TenantId,
                            block.BlockId,
                            snapshot.PropertyId,
                            unit.InventoryUnitId,
                            block.Arrival,
                            block.Departure,
                            block.Status,
                            block.Version),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            var allocations = snapshot.Units
                .SelectMany(unit => unit.Allocations.Select(allocation => new { UnitId = unit.InventoryUnitId, Allocation = allocation }))
                .GroupBy(item => item.Allocation.AllocationId);
            foreach (var allocationGroup in allocations)
            {
                InventoryAllocationProjectionExport allocation = allocationGroup.First().Allocation;
                await repository.ApplyAllocationAsync(
                    new(
                        snapshot.TenantId,
                        allocation.AllocationId,
                        allocation.ReservationId,
                        snapshot.PropertyId,
                        allocation.Arrival,
                        allocation.Departure,
                        allocation.Status,
                        allocationGroup.Select(item => item.UnitId).Distinct().ToArray(),
                        allocation.Version),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ProjectionWriteResult(snapshots.Count);
    }
}
