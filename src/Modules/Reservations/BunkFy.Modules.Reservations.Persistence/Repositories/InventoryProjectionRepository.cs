namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Application.Ports;

internal sealed class InventoryProjectionRepository(ReservationsDbContext dbContext)
    : IInventoryProjectionRepository
{
    public async Task<InventoryUnitSelectionValidation> ValidateSelectionAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken)
    {
        Guid[] requested = inventoryUnitIds.Distinct().ToArray();
        var units = await dbContext.InventoryUnitProjections
            .AsNoTracking()
            .Where(unit => requested.Contains(unit.Id))
            .Select(unit => new { unit.Id, unit.PropertyId })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (units.Length != requested.Length)
        {
            return InventoryUnitSelectionValidation.UnitNotFound;
        }

        return units.Any(unit => unit.PropertyId != propertyId)
            ? InventoryUnitSelectionValidation.PropertyMismatch
            : InventoryUnitSelectionValidation.Valid;
    }

    public async Task ApplyUnitAsync(ReservationInventoryUnitWriteModel unit, CancellationToken cancellationToken)
    {
        ReservationInventoryUnitProjection? projection = await dbContext.InventoryUnitProjections
            .FirstOrDefaultAsync(item => item.Id == unit.InventoryUnitId, cancellationToken)
            .ConfigureAwait(false);
        if (projection is null)
        {
            dbContext.InventoryUnitProjections.Add(ReservationInventoryUnitProjection.Create(unit));
            return;
        }

        projection.Apply(unit);
    }

    public async Task ApplyBlockAsync(ReservationInventoryBlockWriteModel block, CancellationToken cancellationToken)
    {
        ReservationInventoryBlockProjection? projection = await dbContext.InventoryBlockProjections
            .FirstOrDefaultAsync(item => item.Id == block.BlockId, cancellationToken)
            .ConfigureAwait(false);
        if (projection is null)
        {
            dbContext.InventoryBlockProjections.Add(ReservationInventoryBlockProjection.Create(block));
            return;
        }

        projection.Apply(block);
    }

    public async Task ReleaseBlockAsync(
        string scopeId,
        Guid propertyId,
        Guid inventoryUnitId,
        Guid blockId,
        long version,
        CancellationToken cancellationToken)
    {
        ReservationInventoryBlockProjection? projection = await dbContext.InventoryBlockProjections
            .FirstOrDefaultAsync(item => item.Id == blockId, cancellationToken)
            .ConfigureAwait(false);
        if (projection is null)
        {
            dbContext.InventoryBlockProjections.Add(ReservationInventoryBlockProjection.CreateReleasedTombstone(
                scopeId,
                blockId,
                propertyId,
                inventoryUnitId,
                version));
            return;
        }

        projection.Release(propertyId, inventoryUnitId, version);
    }

    public async Task ApplyAllocationAsync(
        ReservationInventoryAllocationWriteModel allocation,
        CancellationToken cancellationToken)
    {
        ReservationInventoryAllocationProjection? projection = await dbContext.InventoryAllocationProjections
            .Include(item => item.Units)
            .FirstOrDefaultAsync(item => item.Id == allocation.AllocationId, cancellationToken)
            .ConfigureAwait(false);
        if (projection is null)
        {
            dbContext.InventoryAllocationProjections.Add(ReservationInventoryAllocationProjection.Create(allocation));
            return;
        }

        projection.Apply(allocation);
    }

    public async Task ReleaseAllocationAsync(
        string scopeId,
        Guid allocationId,
        Guid reservationId,
        long version,
        CancellationToken cancellationToken)
    {
        ReservationInventoryAllocationProjection? projection = await dbContext.InventoryAllocationProjections
            .FirstOrDefaultAsync(item => item.Id == allocationId, cancellationToken)
            .ConfigureAwait(false);
        if (projection is null)
        {
            dbContext.InventoryAllocationProjections.Add(
                ReservationInventoryAllocationProjection.CreateReleasedTombstone(
                    scopeId,
                    allocationId,
                    reservationId,
                    version));
            return;
        }

        projection.Release(reservationId, version);
    }
}
