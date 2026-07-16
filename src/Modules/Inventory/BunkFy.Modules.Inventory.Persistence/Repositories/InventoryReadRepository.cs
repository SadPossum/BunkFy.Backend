namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using Gma.Framework.Pagination;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Properties.Contracts;

internal sealed class InventoryReadRepository(InventoryDbContext dbContext) : IInventoryReadRepository
{
    public Task<bool> PropertyExistsAsync(Guid propertyId, CancellationToken cancellationToken) =>
        dbContext.PropertyTopology.AsNoTracking().AnyAsync(
            property => property.Id == propertyId && property.IsKnown,
            cancellationToken);

    public async Task<RoomInventoryDto?> GetRoomAsync(
        Guid propertyId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        InventoryRoomTopology? room = await dbContext.RoomTopology
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == roomId && item.PropertyId == propertyId && item.IsKnown,
                cancellationToken)
            .ConfigureAwait(false);
        if (room is null)
        {
            return null;
        }

        InventoryPropertyTopology? property = await dbContext.PropertyTopology
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId && item.IsKnown, cancellationToken)
            .ConfigureAwait(false);
        RoomInventoryConfiguration? configuration = await dbContext.RoomConfigurations
            .FirstOrDefaultAsync(item => item.Id == roomId, cancellationToken)
            .ConfigureAwait(false);
        List<InventoryUnit> units = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit => unit.RoomId == roomId && unit.IsKnown)
            .OrderBy(unit => unit.Kind)
            .ThenBy(unit => unit.Label)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        (HashSet<Guid> drainedBedIds, HashSet<Guid> drainingRoomIds, HashSet<Guid> fullyDrainingRoomIds) = await this
            .GetActiveDrainsAsync(propertyId, [roomId], cancellationToken)
            .ConfigureAwait(false);

        return MapRoom(
            room,
            configuration,
            units,
            property?.Status == PropertyStatus.Active,
            drainedBedIds,
            drainingRoomIds,
            fullyDrainingRoomIds);
    }

    public async Task<InventoryUnitSnapshot?> GetUnitAsync(
        Guid propertyId,
        Guid inventoryUnitId,
        CancellationToken cancellationToken)
    {
        InventoryUnit? unit = await dbContext.InventoryUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == inventoryUnitId && item.PropertyId == propertyId && item.IsKnown,
                cancellationToken)
            .ConfigureAwait(false);
        if (unit is null)
        {
            return null;
        }

        InventoryPropertyTopology? property = await dbContext.PropertyTopology
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId && item.IsKnown, cancellationToken)
            .ConfigureAwait(false);
        InventoryRoomTopology? room = await dbContext.RoomTopology
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == unit.RoomId && item.IsKnown, cancellationToken)
            .ConfigureAwait(false);
        RoomInventoryConfiguration? configuration = await dbContext.RoomConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == unit.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (room is null)
        {
            return null;
        }

        (HashSet<Guid> drainedBedIds, HashSet<Guid> drainingRoomIds, HashSet<Guid> fullyDrainingRoomIds) = await this
            .GetActiveDrainsAsync(propertyId, [unit.RoomId], cancellationToken)
            .ConfigureAwait(false);

        InventoryUnitDto mapped = MapUnit(
            unit,
            configuration?.SalesMode ?? RoomSalesMode.Unconfigured,
            property?.Status == PropertyStatus.Active && room.Status == RoomStatus.Active,
            drainedBedIds,
            drainingRoomIds,
            fullyDrainingRoomIds);
        return new(mapped, mapped.IsSellable);
    }

    public async Task<IReadOnlyCollection<InventoryUnitSnapshot>> ResolveBlockTargetUnitsAsync(
        Guid propertyId,
        InventoryBlockTarget target,
        CancellationToken cancellationToken)
    {
        if (target.Kind == InventoryBlockTargetKind.Unit)
        {
            InventoryUnitSnapshot? unit = target.InventoryUnitId.HasValue
                ? await this.GetUnitAsync(propertyId, target.InventoryUnitId.Value, cancellationToken).ConfigureAwait(false)
                : null;
            return unit is null ? [] : [unit];
        }

        InventoryPropertyTopology? property = await dbContext.PropertyTopology
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId && item.IsKnown, cancellationToken)
            .ConfigureAwait(false);
        if (property is null)
        {
            return [];
        }

        IQueryable<InventoryRoomTopology> roomQuery = dbContext.RoomTopology
            .AsNoTracking()
            .Where(room => room.PropertyId == propertyId && room.IsKnown);
        string? buildingLabel = target.BuildingLabel?.Trim();
        string? floorLabel = target.FloorLabel?.Trim();
        roomQuery = target.Kind switch
        {
            InventoryBlockTargetKind.Property => roomQuery,
            InventoryBlockTargetKind.Building when buildingLabel is not null => roomQuery.Where(
                room => room.BuildingLabel == buildingLabel),
            InventoryBlockTargetKind.Floor when buildingLabel is null && floorLabel is not null => roomQuery.Where(
                room => room.BuildingLabel == null && room.FloorLabel == floorLabel),
            InventoryBlockTargetKind.Floor when floorLabel is not null => roomQuery.Where(
                room => room.BuildingLabel == buildingLabel && room.FloorLabel == floorLabel),
            InventoryBlockTargetKind.Room when target.RoomId.HasValue => roomQuery.Where(
                room => room.Id == target.RoomId.Value),
            _ => roomQuery.Where(_ => false)
        };

        InventoryRoomTopology[] rooms = await roomQuery
            .OrderBy(room => room.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] roomIds = rooms.Select(room => room.Id).ToArray();
        if (roomIds.Length == 0)
        {
            return [];
        }

        Dictionary<Guid, RoomInventoryConfiguration> configurations = await dbContext.RoomConfigurations
            .AsNoTracking()
            .Where(configuration => roomIds.Contains(configuration.Id))
            .ToDictionaryAsync(configuration => configuration.Id, cancellationToken)
            .ConfigureAwait(false);
        InventoryUnit[] units = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit => roomIds.Contains(unit.RoomId) && unit.IsKnown)
            .OrderBy(unit => unit.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, InventoryRoomTopology> roomsById = rooms.ToDictionary(room => room.Id);
        (HashSet<Guid> drainedBedIds, HashSet<Guid> drainingRoomIds, HashSet<Guid> fullyDrainingRoomIds) = await this
            .GetActiveDrainsAsync(propertyId, roomIds, cancellationToken)
            .ConfigureAwait(false);

        return units
            .Select(unit =>
            {
                InventoryRoomTopology room = roomsById[unit.RoomId];
                RoomSalesMode salesMode = configurations.GetValueOrDefault(unit.RoomId)?.SalesMode ?? RoomSalesMode.Unconfigured;
                InventoryUnitDto mapped = MapUnit(
                    unit,
                    salesMode,
                    property.Status == PropertyStatus.Active && room.Status == RoomStatus.Active,
                    drainedBedIds,
                    drainingRoomIds,
                    fullyDrainingRoomIds);
                return new InventoryUnitSnapshot(mapped, mapped.IsSellable);
            })
            .ToArray();
    }

    public async Task<RoomInventoryListResponse> ListRoomsAsync(
        Guid propertyId,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        InventoryPropertyTopology? property = await dbContext.PropertyTopology
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId && item.IsKnown, cancellationToken)
            .ConfigureAwait(false);
        if (property is null)
        {
            return new([], pageRequest.Page, pageRequest.PageSize);
        }

        List<InventoryRoomTopology> rooms = await dbContext.RoomTopology
            .AsNoTracking()
            .Where(room => room.PropertyId == propertyId && room.IsKnown)
            .OrderBy(room => room.Name)
            .ThenBy(room => room.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] roomIds = rooms.Select(room => room.Id).ToArray();
        Dictionary<Guid, RoomInventoryConfiguration> configurations = await dbContext.RoomConfigurations
            .AsNoTracking()
            .Where(configuration => roomIds.Contains(configuration.Id))
            .ToDictionaryAsync(configuration => configuration.Id, cancellationToken)
            .ConfigureAwait(false);
        List<InventoryUnit> units = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit => roomIds.Contains(unit.RoomId) && unit.IsKnown)
            .OrderBy(unit => unit.Kind)
            .ThenBy(unit => unit.Label)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        ILookup<Guid, InventoryUnit> unitsByRoom = units.ToLookup(unit => unit.RoomId);
        (HashSet<Guid> drainedBedIds, HashSet<Guid> drainingRoomIds, HashSet<Guid> fullyDrainingRoomIds) = await this
            .GetActiveDrainsAsync(propertyId, roomIds, cancellationToken)
            .ConfigureAwait(false);

        RoomInventoryDto[] result = rooms
            .Select(room => MapRoom(
                room,
                configurations.GetValueOrDefault(room.Id),
                unitsByRoom[room.Id],
                property.Status == PropertyStatus.Active,
                drainedBedIds,
                drainingRoomIds,
                fullyDrainingRoomIds))
            .ToArray();
        return new(result, pageRequest.Page, pageRequest.PageSize);
    }

    public async Task<InventoryAvailabilityResponse> GetAvailabilityAsync(
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken)
    {
        InventoryPropertyTopology? property = await dbContext.PropertyTopology
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId && item.IsKnown, cancellationToken)
            .ConfigureAwait(false);
        if (property is null)
        {
            return new(propertyId, arrival, departure, []);
        }

        List<InventoryRoomTopology> rooms = await dbContext.RoomTopology
            .AsNoTracking()
            .Where(room => room.PropertyId == propertyId && room.IsKnown)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] roomIds = rooms.Select(room => room.Id).ToArray();
        Dictionary<Guid, RoomInventoryConfiguration> configurations = await dbContext.RoomConfigurations
            .AsNoTracking()
            .Where(configuration => roomIds.Contains(configuration.Id))
            .ToDictionaryAsync(configuration => configuration.Id, cancellationToken)
            .ConfigureAwait(false);
        List<InventoryUnit> units = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit => unit.PropertyId == propertyId && unit.IsKnown)
            .OrderBy(unit => unit.RoomId)
            .ThenBy(unit => unit.Kind)
            .ThenBy(unit => unit.Label)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] unitIds = units.Select(unit => unit.Id).ToArray();
        var activeBlocks = await dbContext.ManualBlocks
            .AsNoTracking()
            .Where(block => unitIds.Contains(block.InventoryUnitId) &&
                            block.Status == ManualInventoryBlockState.Active &&
                            block.Arrival < departure &&
                            arrival < block.Departure)
            .Select(block => new { block.InventoryUnitId, block.Id })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        ILookup<Guid, Guid> blocksByUnit = activeBlocks.ToLookup(block => block.InventoryUnitId, block => block.Id);
        var activeAllocations = await dbContext.AllocationUnits
            .AsNoTracking()
            .Where(unit => unitIds.Contains(unit.Id))
            .Join(
                dbContext.Allocations.AsNoTracking().Where(allocation =>
                    allocation.Status == InventoryAllocationState.Active &&
                    allocation.Arrival < departure &&
                    arrival < allocation.Departure),
                unit => unit.AllocationId,
                allocation => allocation.Id,
                (unit, allocation) => new { InventoryUnitId = unit.Id, AllocationId = allocation.Id })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        ILookup<Guid, Guid> allocationsByUnit = activeAllocations.ToLookup(
            allocation => allocation.InventoryUnitId,
            allocation => allocation.AllocationId);
        Dictionary<Guid, InventoryRoomTopology> roomsById = rooms.ToDictionary(room => room.Id);
        ILookup<Guid, InventoryUnit> unitsByRoom = units.ToLookup(unit => unit.RoomId);
        (HashSet<Guid> drainedBedIds, HashSet<Guid> drainingRoomIds, HashSet<Guid> fullyDrainingRoomIds) = await this
            .GetActiveDrainsAsync(propertyId, roomIds, cancellationToken)
            .ConfigureAwait(false);

        InventoryUnitAvailabilityDto[] availability = units
            .Select(unit =>
            {
                bool roomActive = roomsById.TryGetValue(unit.RoomId, out InventoryRoomTopology? room) &&
                                  property.Status == PropertyStatus.Active &&
                                  room.Status == RoomStatus.Active;
                RoomSalesMode mode = configurations.GetValueOrDefault(unit.RoomId)?.SalesMode ?? RoomSalesMode.Unconfigured;
                InventoryUnitDto mapped = MapUnit(
                    unit,
                    mode,
                    roomActive,
                    drainedBedIds,
                    drainingRoomIds,
                    fullyDrainingRoomIds);
                Guid[] conflictUnitIds = unit.Kind == InventoryUnitKind.Room
                    ? unitsByRoom[unit.RoomId].Select(item => item.Id).ToArray()
                    : unitsByRoom[unit.RoomId]
                        .Where(item => item.Id == unit.Id || item.Kind == InventoryUnitKind.Room)
                        .Select(item => item.Id)
                        .ToArray();
                Guid[] blockingIds = conflictUnitIds
                    .SelectMany(conflictUnitId => blocksByUnit[conflictUnitId])
                    .Distinct()
                    .ToArray();
                Guid[] allocationIds = conflictUnitIds
                    .SelectMany(conflictUnitId => allocationsByUnit[conflictUnitId])
                    .Distinct()
                    .ToArray();
                return new InventoryUnitAvailabilityDto(
                    mapped,
                    mapped.IsSellable && blockingIds.Length == 0 && allocationIds.Length == 0,
                    blockingIds,
                    allocationIds);
            })
            .Where(item => item.Unit.IsSellable)
            .ToArray();

        return new(propertyId, arrival, departure, availability);
    }

    private static RoomInventoryDto MapRoom(
        InventoryRoomTopology room,
        RoomInventoryConfiguration? configuration,
        IEnumerable<InventoryUnit> units,
        bool propertyActive,
        IReadOnlySet<Guid>? drainedBedIds = null,
        IReadOnlySet<Guid>? drainingRoomIds = null,
        IReadOnlySet<Guid>? fullyDrainingRoomIds = null)
    {
        RoomSalesMode salesMode = configuration?.SalesMode ?? RoomSalesMode.Unconfigured;
        bool roomActive = propertyActive && room.Status == RoomStatus.Active;
        InventoryUnitDto[] mappedUnits = units
            .Select(unit => MapUnit(
                unit,
                salesMode,
                roomActive,
                drainedBedIds,
                drainingRoomIds,
                fullyDrainingRoomIds))
            .ToArray();

        return new(
            room.PropertyId,
            room.Id,
            room.Name,
            room.BuildingLabel,
            room.FloorLabel,
            salesMode switch
            {
                RoomSalesMode.RoomLevel => InventorySalesMode.RoomLevel,
                RoomSalesMode.BedLevel => InventorySalesMode.BedLevel,
                _ => InventorySalesMode.Unconfigured
            },
            configuration?.Version ?? 1,
            mappedUnits);
    }

    private static InventoryUnitDto MapUnit(
        InventoryUnit unit,
        RoomSalesMode salesMode,
        bool roomActive,
        IReadOnlySet<Guid>? drainedBedIds = null,
        IReadOnlySet<Guid>? drainingRoomIds = null,
        IReadOnlySet<Guid>? fullyDrainingRoomIds = null)
    {
        bool topologyActive = roomActive && unit.IsTopologyActive;
        bool sellable = topologyActive &&
                        ((unit.Kind == InventoryUnitKind.Room && salesMode == RoomSalesMode.RoomLevel) ||
                         (unit.Kind == InventoryUnitKind.Bed && salesMode == RoomSalesMode.BedLevel)) &&
                        (unit.Kind != InventoryUnitKind.Room || drainingRoomIds?.Contains(unit.RoomId) != true) &&
                        (unit.Kind != InventoryUnitKind.Bed ||
                         (drainedBedIds?.Contains(unit.Id) != true &&
                          fullyDrainingRoomIds?.Contains(unit.RoomId) != true));
        return new(
            unit.Id,
            unit.PropertyId,
            unit.RoomId,
            unit.BedId,
            unit.Kind,
            unit.Label,
            sellable,
            topologyActive);
    }

    private async Task<(HashSet<Guid> BedIds, HashSet<Guid> RoomIds, HashSet<Guid> FullyDrainingRoomIds)> GetActiveDrainsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> roomIds,
        CancellationToken cancellationToken)
    {
        BedRetirementProcess[] drains = await dbContext.BedRetirements
            .AsNoTracking()
            .Where(process =>
                process.PropertyId == propertyId &&
                roomIds.Contains(process.RoomId) &&
                (process.State == InventoryRetirementProcessState.Draining ||
                 process.State == InventoryRetirementProcessState.FinalizationRequested ||
                 process.State == InventoryRetirementProcessState.FinalizedAwaitingTopology ||
                 process.State == InventoryRetirementProcessState.Rejected))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (BedRetirementProcess local in dbContext.BedRetirements.Local.Where(process =>
                     process.PropertyId == propertyId &&
                     roomIds.Contains(process.RoomId) &&
                     BedRetirementProcess.IsDrainActive(process.State)))
        {
            if (drains.All(process => process.Id != local.Id))
            {
                drains = [.. drains, local];
            }
        }

        RoomRetirementProcess[] roomDrains = await dbContext.RoomRetirements
            .AsNoTracking()
            .Where(process =>
                process.PropertyId == propertyId &&
                roomIds.Contains(process.RoomId) &&
                (process.State == InventoryRetirementProcessState.Draining ||
                 process.State == InventoryRetirementProcessState.FinalizationRequested ||
                 process.State == InventoryRetirementProcessState.FinalizedAwaitingTopology ||
                 process.State == InventoryRetirementProcessState.Rejected))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (RoomRetirementProcess local in dbContext.RoomRetirements.Local.Where(process =>
                     process.PropertyId == propertyId &&
                     roomIds.Contains(process.RoomId) &&
                     RoomRetirementProcess.IsDrainActive(process.State)))
        {
            if (roomDrains.All(process => process.Id != local.Id))
            {
                roomDrains = [.. roomDrains, local];
            }
        }

        HashSet<Guid> fullyDrainingRoomIds = roomDrains.Select(process => process.RoomId).ToHashSet();

        return (
            drains.Select(process => process.BedId).ToHashSet(),
            drains.Select(process => process.RoomId).Concat(fullyDrainingRoomIds).ToHashSet(),
            fullyDrainingRoomIds);
    }
}
