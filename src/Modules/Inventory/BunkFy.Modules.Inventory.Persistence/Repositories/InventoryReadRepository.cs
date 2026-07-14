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

        return MapRoom(room, configuration, units, property?.Status == PropertyStatus.Active);
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

        InventoryUnitDto mapped = MapUnit(
            unit,
            configuration?.SalesMode ?? RoomSalesMode.Unconfigured,
            property?.Status == PropertyStatus.Active && room.Status == RoomStatus.Active);
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

        return units
            .Select(unit =>
            {
                InventoryRoomTopology room = roomsById[unit.RoomId];
                RoomSalesMode salesMode = configurations.GetValueOrDefault(unit.RoomId)?.SalesMode ?? RoomSalesMode.Unconfigured;
                InventoryUnitDto mapped = MapUnit(
                    unit,
                    salesMode,
                    property.Status == PropertyStatus.Active && room.Status == RoomStatus.Active);
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

        RoomInventoryDto[] result = rooms
            .Select(room => MapRoom(
                room,
                configurations.GetValueOrDefault(room.Id),
                unitsByRoom[room.Id],
                property.Status == PropertyStatus.Active))
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

        InventoryUnitAvailabilityDto[] availability = units
            .Select(unit =>
            {
                bool roomActive = roomsById.TryGetValue(unit.RoomId, out InventoryRoomTopology? room) &&
                                  property.Status == PropertyStatus.Active &&
                                  room.Status == RoomStatus.Active;
                RoomSalesMode mode = configurations.GetValueOrDefault(unit.RoomId)?.SalesMode ?? RoomSalesMode.Unconfigured;
                InventoryUnitDto mapped = MapUnit(unit, mode, roomActive);
                Guid[] blockingIds = blocksByUnit[unit.Id].ToArray();
                Guid[] allocationIds = allocationsByUnit[unit.Id].ToArray();
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
        bool propertyActive)
    {
        RoomSalesMode salesMode = configuration?.SalesMode ?? RoomSalesMode.Unconfigured;
        bool roomActive = propertyActive && room.Status == RoomStatus.Active;
        InventoryUnitDto[] mappedUnits = units
            .Select(unit => MapUnit(unit, salesMode, roomActive))
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
        bool roomActive)
    {
        bool topologyActive = roomActive && unit.IsTopologyActive;
        bool sellable = topologyActive &&
                        ((unit.Kind == InventoryUnitKind.Room && salesMode == RoomSalesMode.RoomLevel) ||
                         (unit.Kind == InventoryUnitKind.Bed && salesMode == RoomSalesMode.BedLevel));
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
}
