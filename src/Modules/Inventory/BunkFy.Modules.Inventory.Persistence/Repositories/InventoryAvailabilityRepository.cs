namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Properties.Contracts;

internal sealed class InventoryAvailabilityRepository(InventoryDbContext dbContext)
    : IInventoryAvailabilityRepository
{
    public async Task<InventoryAvailabilityContextSnapshot> GetContextAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = inventoryUnitIds.Distinct().ToArray();
        InventoryRetirementProcessState[] activeStates =
        [
            InventoryRetirementProcessState.Draining,
            InventoryRetirementProcessState.FinalizationRequested,
            InventoryRetirementProcessState.FinalizedAwaitingTopology,
            InventoryRetirementProcessState.Rejected
        ];
        BedRetirementProcess[] trackedBedRetirements = dbContext.ChangeTracker
            .Entries<BedRetirementProcess>()
            .Where(entry => entry.State != EntityState.Detached)
            .Select(entry => entry.Entity)
            .ToArray();
        Guid[] trackedBedRetirementIds = trackedBedRetirements.Select(process => process.Id).ToArray();
        BedRetirementProcess[] activeTrackedBedRetirements = trackedBedRetirements
            .Where(process => BedRetirementProcess.IsDrainActive(process.State))
            .ToArray();
        RoomRetirementProcess[] trackedRoomRetirements = dbContext.ChangeTracker
            .Entries<RoomRetirementProcess>()
            .Where(entry => entry.State != EntityState.Detached)
            .Select(entry => entry.Entity)
            .ToArray();
        Guid[] trackedRoomRetirementIds = trackedRoomRetirements.Select(process => process.Id).ToArray();
        Guid[] activeTrackedRoomIds = trackedRoomRetirements
            .Where(process => RoomRetirementProcess.IsDrainActive(process.State))
            .Select(process => process.RoomId)
            .Distinct()
            .ToArray();
        var requestedUnits = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit => ids.Contains(unit.Id) && unit.PropertyId == propertyId && unit.IsKnown)
            .Select(unit => new
            {
                unit.Id,
                unit.RoomId,
                unit.Kind,
                TopologyActive = unit.IsTopologyActive &&
                    dbContext.PropertyTopology.Any(property =>
                        property.Id == propertyId &&
                        property.IsKnown &&
                        property.Status == PropertyStatus.Active) &&
                    dbContext.RoomTopology.Any(room =>
                        room.Id == unit.RoomId &&
                        room.IsKnown &&
                        room.Status == RoomStatus.Active),
                SalesMode = dbContext.RoomConfigurations
                    .Where(configuration => configuration.Id == unit.RoomId)
                    .Select(configuration => configuration.SalesMode)
                    .FirstOrDefault(),
                HasPersistedBedDrain = dbContext.BedRetirements.Any(process =>
                    !trackedBedRetirementIds.Contains(process.Id) &&
                    process.PropertyId == propertyId &&
                    process.RoomId == unit.RoomId &&
                    activeStates.Contains(process.State) &&
                    (process.BedId == unit.Id || unit.Kind == InventoryUnitKind.Room)),
                HasPersistedRoomDrain = dbContext.RoomRetirements.Any(process =>
                    !trackedRoomRetirementIds.Contains(process.Id) &&
                    process.PropertyId == propertyId &&
                    process.RoomId == unit.RoomId &&
                    activeStates.Contains(process.State))
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] roomLevelRoomIds = requestedUnits
            .Where(unit => unit.Kind == InventoryUnitKind.Room)
            .Select(unit => unit.RoomId)
            .Distinct()
            .ToArray();
        Guid[] bedLevelRoomIds = requestedUnits
            .Where(unit => unit.Kind == InventoryUnitKind.Bed)
            .Select(unit => unit.RoomId)
            .Distinct()
            .ToArray();
        Guid[] bedIds = requestedUnits
            .Where(unit => unit.Kind == InventoryUnitKind.Bed)
            .Select(unit => unit.Id)
            .ToArray();
        Guid[] conflictUnitIds = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit =>
                unit.PropertyId == propertyId &&
                (roomLevelRoomIds.Contains(unit.RoomId) ||
                 bedIds.Contains(unit.Id) ||
                 (unit.Kind == InventoryUnitKind.Room && bedLevelRoomIds.Contains(unit.RoomId))))
            .Select(unit => unit.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        InventoryAllocationUnitSnapshot[] units = requestedUnits
            .Select(unit => new InventoryAllocationUnitSnapshot(
                unit.Id,
                unit.TopologyActive,
                unit.TopologyActive &&
                ((unit.Kind == InventoryUnitKind.Room && unit.SalesMode == RoomSalesMode.RoomLevel) ||
                 (unit.Kind == InventoryUnitKind.Bed && unit.SalesMode == RoomSalesMode.BedLevel)) &&
                !unit.HasPersistedBedDrain &&
                !activeTrackedBedRetirements.Any(process =>
                    process.PropertyId == propertyId &&
                    process.RoomId == unit.RoomId &&
                    (process.BedId == unit.Id || unit.Kind == InventoryUnitKind.Room)) &&
                !unit.HasPersistedRoomDrain &&
                !activeTrackedRoomIds.Contains(unit.RoomId)))
            .ToArray();
        return new InventoryAvailabilityContextSnapshot(units, conflictUnitIds);
    }

    public async Task<InventoryAvailabilityConflictSnapshot> GetConflictsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> conflictUnitIds,
        DateOnly arrival,
        DateOnly departure,
        Guid? excludedAllocationId,
        IReadOnlyCollection<Guid> excludedBlockIds,
        CancellationToken cancellationToken)
    {
        Guid[] unitIds = conflictUnitIds.Distinct().ToArray();
        Guid[] excludedIds = excludedBlockIds.Distinct().ToArray();
        var conflicts = await dbContext.PropertyTopology
            .AsNoTracking()
            .Where(property => property.Id == propertyId)
            .Select(_ => new
            {
                HasManualBlockConflict = dbContext.ManualBlocks.Any(block =>
                    block.PropertyId == propertyId &&
                    unitIds.Contains(block.InventoryUnitId) &&
                    !excludedIds.Contains(block.Id) &&
                    block.Status == ManualInventoryBlockState.Active &&
                    block.Arrival < departure &&
                    arrival < block.Departure),
                HasActiveAllocationConflict = dbContext.Allocations.Any(allocation =>
                    allocation.PropertyId == propertyId &&
                    allocation.Status == InventoryAllocationState.Active &&
                    (!excludedAllocationId.HasValue || allocation.Id != excludedAllocationId.Value) &&
                    allocation.Arrival < departure &&
                    arrival < allocation.Departure &&
                    allocation.Units.Any(unit => unitIds.Contains(unit.Id)))
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return conflicts is null
            ? new(false, false)
            : new(conflicts.HasManualBlockConflict, conflicts.HasActiveAllocationConflict);
    }

    public async Task<RoomInventoryImpactSnapshot?> GetRoomImpactAsync(
        Guid propertyId,
        Guid roomId,
        CancellationToken cancellationToken) =>
        await this.GetRoomImpactAsync(
            propertyId,
            roomId,
            excludedAllocationId: null,
            excludedBlockIds: [],
            cancellationToken).ConfigureAwait(false);

    public async Task<RoomInventoryImpactSnapshot?> GetRoomImpactAsync(
        Guid propertyId,
        Guid roomId,
        Guid? excludedAllocationId,
        IReadOnlyCollection<Guid> excludedBlockIds,
        CancellationToken cancellationToken)
    {
        bool roomExists = await dbContext.RoomTopology
            .AsNoTracking()
            .AnyAsync(room => room.Id == roomId && room.PropertyId == propertyId && room.IsKnown, cancellationToken)
            .ConfigureAwait(false);
        if (!roomExists)
        {
            return null;
        }

        Guid[] roomUnitIds = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit => unit.PropertyId == propertyId && unit.RoomId == roomId)
            .Select(unit => unit.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        IQueryable<Guid> reservationIdsQuery = dbContext.Allocations
            .AsNoTracking()
            .Where(allocation =>
                allocation.PropertyId == propertyId &&
                allocation.Status == InventoryAllocationState.Active &&
                (!excludedAllocationId.HasValue || allocation.Id != excludedAllocationId.Value) &&
                allocation.Units.Any(unit => roomUnitIds.Contains(unit.Id)))
            .Select(allocation => allocation.ReservationId)
            .Distinct()
            .OrderBy(reservationId => reservationId);
        int allocationCount = await reservationIdsQuery
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] reservationIds = await reservationIdsQuery
            .Take(InventoryImpactLimits.AffectedReservationSampleSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] excludedIds = excludedBlockIds.Distinct().ToArray();
        int blockCount = await dbContext.ManualBlocks
            .AsNoTracking()
            .Where(block =>
                block.PropertyId == propertyId &&
                block.Status == ManualInventoryBlockState.Active &&
                !excludedIds.Contains(block.Id) &&
                roomUnitIds.Contains(block.InventoryUnitId))
            .Select(block => block.BlockGroupId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        int retirementCount = await dbContext.BedRetirements
            .AsNoTracking()
            .CountAsync(process =>
                process.PropertyId == propertyId &&
                process.RoomId == roomId &&
                (process.State == InventoryRetirementProcessState.Draining ||
                 process.State == InventoryRetirementProcessState.FinalizationRequested ||
                 process.State == InventoryRetirementProcessState.FinalizedAwaitingTopology ||
                 process.State == InventoryRetirementProcessState.Rejected),
                cancellationToken)
            .ConfigureAwait(false);
        int roomRetirementCount = await dbContext.RoomRetirements
            .AsNoTracking()
            .CountAsync(process =>
                process.PropertyId == propertyId &&
                process.RoomId == roomId &&
                (process.State == InventoryRetirementProcessState.Draining ||
                 process.State == InventoryRetirementProcessState.FinalizationRequested ||
                 process.State == InventoryRetirementProcessState.FinalizedAwaitingTopology ||
                 process.State == InventoryRetirementProcessState.Rejected),
                cancellationToken)
            .ConfigureAwait(false);
        return new RoomInventoryImpactSnapshot(
            allocationCount,
            blockCount,
            retirementCount,
            roomRetirementCount,
            reservationIds,
            allocationCount > reservationIds.Length);
    }

    public async Task<BedRetirementImpactSnapshot?> GetBedRetirementImpactAsync(
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        Guid? excludedAllocationId,
        IReadOnlyCollection<Guid> excludedBlockIds,
        CancellationToken cancellationToken)
    {
        var target = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit =>
                unit.Id == bedId &&
                unit.PropertyId == propertyId &&
                unit.RoomId == roomId &&
                unit.Kind == InventoryUnitKind.Bed &&
                unit.IsKnown)
            .Select(unit => new { unit.Id })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (target is null)
        {
            return null;
        }

        Guid[] conflictUnitIds = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit =>
                unit.PropertyId == propertyId &&
                unit.RoomId == roomId &&
                (unit.Id == bedId || unit.Kind == InventoryUnitKind.Room))
            .Select(unit => unit.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        IQueryable<Guid> reservationIdsQuery = dbContext.Allocations
            .AsNoTracking()
            .Where(allocation =>
                allocation.PropertyId == propertyId &&
                allocation.Status == InventoryAllocationState.Active &&
                (!excludedAllocationId.HasValue || allocation.Id != excludedAllocationId.Value) &&
                allocation.Units.Any(unit => conflictUnitIds.Contains(unit.Id)))
            .Select(allocation => allocation.ReservationId)
            .Distinct()
            .OrderBy(reservationId => reservationId);
        int allocationCount = await reservationIdsQuery
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] reservationIds = await reservationIdsQuery
            .Take(InventoryImpactLimits.AffectedReservationSampleSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] excludedIds = excludedBlockIds.Distinct().ToArray();
        int blockCount = await dbContext.ManualBlocks
            .AsNoTracking()
            .Where(block =>
                block.PropertyId == propertyId &&
                block.Status == ManualInventoryBlockState.Active &&
                !excludedIds.Contains(block.Id) &&
                conflictUnitIds.Contains(block.InventoryUnitId))
            .Select(block => block.BlockGroupId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        return new BedRetirementImpactSnapshot(
            allocationCount,
            blockCount,
            reservationIds,
            allocationCount > reservationIds.Length);
    }

    public async Task TouchUnitsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = inventoryUnitIds.Distinct().Order().ToArray();
        InventoryUnit[] units = await dbContext.InventoryUnits
            .Where(unit => unit.PropertyId == propertyId && ids.Contains(unit.Id))
            .OrderBy(unit => unit.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (units.Length != ids.Length)
        {
            throw new InvalidOperationException("Cannot serialize availability changes for missing inventory units.");
        }

        Guid[] roomIds = units.Select(unit => unit.RoomId).Distinct().Order().ToArray();
        RoomInventoryConfiguration[] rooms = await dbContext.RoomConfigurations
            .Where(configuration =>
                configuration.PropertyId == propertyId &&
                roomIds.Contains(configuration.Id))
            .OrderBy(configuration => configuration.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rooms.Length != roomIds.Length)
        {
            throw new InvalidOperationException("Cannot serialize availability changes for missing room configurations.");
        }

        foreach (InventoryUnit unit in units)
        {
            unit.TouchAvailability();
        }

        foreach (RoomInventoryConfiguration room in rooms)
        {
            room.TouchAvailability();
        }
    }

}
