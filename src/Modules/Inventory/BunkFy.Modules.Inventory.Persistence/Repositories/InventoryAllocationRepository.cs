namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Properties.Contracts;

internal sealed class InventoryAllocationRepository(InventoryDbContext dbContext)
    : IInventoryAllocationRepository
{
    public Task<InventoryAllocation?> GetByRequestAsync(
        Guid allocationRequestId,
        CancellationToken cancellationToken) =>
        dbContext.Allocations
            .Include(allocation => allocation.Units)
            .FirstOrDefaultAsync(allocation => allocation.AllocationRequestId == allocationRequestId, cancellationToken);

    public Task<InventoryAllocation?> GetByReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.Allocations
            .Include(allocation => allocation.Units)
            .FirstOrDefaultAsync(allocation => allocation.ReservationId == reservationId, cancellationToken);

    public Task<InventoryAllocation?> GetAsync(Guid allocationId, CancellationToken cancellationToken) =>
        dbContext.Allocations
            .Include(allocation => allocation.Units)
            .FirstOrDefaultAsync(allocation => allocation.Id == allocationId, cancellationToken);

    public Task AddAsync(InventoryAllocation allocation, CancellationToken cancellationToken)
    {
        dbContext.Allocations.Add(allocation);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<InventoryAllocationUnitSnapshot>> GetUnitsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = inventoryUnitIds.Distinct().ToArray();
        bool propertyActive = await dbContext.PropertyTopology
            .AsNoTracking()
            .AnyAsync(property =>
                property.Id == propertyId &&
                property.IsKnown &&
                property.Status == PropertyStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);
        List<InventoryUnit> units = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit => ids.Contains(unit.Id) && unit.PropertyId == propertyId && unit.IsKnown)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] roomIds = units.Select(unit => unit.RoomId).Distinct().ToArray();
        Dictionary<Guid, RoomStatus> roomStatuses = await dbContext.RoomTopology
            .AsNoTracking()
            .Where(room => roomIds.Contains(room.Id) && room.IsKnown)
            .ToDictionaryAsync(room => room.Id, room => room.Status, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, RoomSalesMode> salesModes = await dbContext.RoomConfigurations
            .AsNoTracking()
            .Where(configuration => roomIds.Contains(configuration.Id))
            .ToDictionaryAsync(configuration => configuration.Id, configuration => configuration.SalesMode, cancellationToken)
            .ConfigureAwait(false);

        return units.Select(unit =>
        {
            bool topologyActive = propertyActive &&
                                  unit.IsTopologyActive &&
                                  roomStatuses.GetValueOrDefault(unit.RoomId) == RoomStatus.Active;
            RoomSalesMode salesMode = salesModes.GetValueOrDefault(unit.RoomId, RoomSalesMode.Unconfigured);
            bool sellable = topologyActive &&
                            ((unit.Kind == InventoryUnitKind.Room && salesMode == RoomSalesMode.RoomLevel) ||
                             (unit.Kind == InventoryUnitKind.Bed && salesMode == RoomSalesMode.BedLevel));
            return new InventoryAllocationUnitSnapshot(unit.Id, topologyActive, sellable);
        }).ToArray();
    }

    public Task<bool> HasManualBlockConflictAsync(
        IReadOnlyCollection<Guid> inventoryUnitIds,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken)
    {
        Guid[] ids = inventoryUnitIds.Distinct().ToArray();
        return dbContext.ManualBlocks.AnyAsync(
            block => ids.Contains(block.InventoryUnitId) &&
                     block.Status == ManualInventoryBlockState.Active &&
                     block.Arrival < departure &&
                     arrival < block.Departure,
            cancellationToken);
    }

    public Task<bool> HasActiveAllocationConflictAsync(
        IReadOnlyCollection<Guid> inventoryUnitIds,
        DateOnly arrival,
        DateOnly departure,
        Guid? excludedAllocationId,
        CancellationToken cancellationToken)
    {
        Guid[] ids = inventoryUnitIds.Distinct().ToArray();
        return dbContext.Allocations.AnyAsync(
            allocation => allocation.Status == InventoryAllocationState.Active &&
                          (!excludedAllocationId.HasValue || allocation.Id != excludedAllocationId.Value) &&
                          allocation.Arrival < departure &&
                          arrival < allocation.Departure &&
                          allocation.Units.Any(unit => ids.Contains(unit.Id)),
            cancellationToken);
    }

    public async Task TouchUnitsAsync(
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = inventoryUnitIds.Distinct().Order().ToArray();
        List<InventoryUnit> units = await dbContext.InventoryUnits
            .Where(unit => ids.Contains(unit.Id))
            .OrderBy(unit => unit.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (units.Count != ids.Length)
        {
            throw new InvalidOperationException("Cannot serialize availability changes for missing inventory units.");
        }

        foreach (InventoryUnit unit in units)
        {
            unit.TouchAvailability();
        }
    }
}
