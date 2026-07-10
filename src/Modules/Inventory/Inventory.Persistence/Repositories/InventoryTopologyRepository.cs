namespace Inventory.Persistence.Repositories;

using Inventory.Application.Ports;
using Inventory.Contracts;
using Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Properties.Contracts;

internal sealed class InventoryTopologyRepository(InventoryDbContext dbContext) : IInventoryTopologyRepository
{
    public async Task ApplyPropertyAsync(
        InventoryPropertyTopologyWriteModel property,
        CancellationToken cancellationToken)
    {
        InventoryPropertyTopology? projection = dbContext.PropertyTopology.Local
            .FirstOrDefault(item => item.Id == property.PropertyId && item.ScopeId == property.ScopeId) ??
            await dbContext.PropertyTopology
                .FirstOrDefaultAsync(item => item.Id == property.PropertyId, cancellationToken)
                .ConfigureAwait(false);

        if (projection is null)
        {
            projection = InventoryPropertyTopology.Create(property.PropertyId, property.ScopeId);
            dbContext.PropertyTopology.Add(projection);
        }

        projection.Apply(
            property.Name,
            property.Code,
            property.TimeZoneId,
            property.Status,
            property.SourceVersion);
    }

    public async Task ApplyRoomAsync(InventoryRoomTopologyWriteModel room, CancellationToken cancellationToken)
    {
        await this.EnsurePropertyPlaceholderAsync(room.ScopeId, room.PropertyId, cancellationToken).ConfigureAwait(false);
        InventoryRoomTopology? projection = dbContext.RoomTopology.Local
            .FirstOrDefault(item => item.Id == room.RoomId && item.ScopeId == room.ScopeId) ??
            await dbContext.RoomTopology
                .FirstOrDefaultAsync(item => item.Id == room.RoomId, cancellationToken)
                .ConfigureAwait(false);

        if (projection is null)
        {
            projection = InventoryRoomTopology.Create(room.RoomId, room.ScopeId, room.PropertyId);
            dbContext.RoomTopology.Add(projection);
        }

        projection.Apply(
            room.PropertyId,
            room.Name,
            room.BuildingLabel,
            room.FloorLabel,
            room.Status,
            room.SourceVersion);
        await this.ApplyInventoryUnitAsync(
            room.ScopeId,
            room.PropertyId,
            room.RoomId,
            null,
            InventoryUnitKind.Room,
            room.Name,
            room.Status == RoomStatus.Active,
            room.SourceVersion,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyBedAsync(InventoryBedTopologyWriteModel bed, CancellationToken cancellationToken)
    {
        await this.EnsurePropertyPlaceholderAsync(bed.ScopeId, bed.PropertyId, cancellationToken).ConfigureAwait(false);
        await this.EnsureRoomPlaceholderAsync(
            bed.ScopeId,
            bed.PropertyId,
            bed.RoomId,
            cancellationToken).ConfigureAwait(false);

        InventoryBedTopology? projection = dbContext.BedTopology.Local
            .FirstOrDefault(item => item.Id == bed.BedId && item.ScopeId == bed.ScopeId) ??
            await dbContext.BedTopology
                .FirstOrDefaultAsync(item => item.Id == bed.BedId, cancellationToken)
                .ConfigureAwait(false);
        if (projection is null)
        {
            projection = InventoryBedTopology.Create(bed.BedId, bed.ScopeId, bed.PropertyId, bed.RoomId);
            dbContext.BedTopology.Add(projection);
        }

        projection.Apply(bed.PropertyId, bed.RoomId, bed.Label, bed.Status, bed.BedSourceVersion);
        await this.ApplyInventoryUnitAsync(
            bed.ScopeId,
            bed.PropertyId,
            bed.RoomId,
            bed.BedId,
            InventoryUnitKind.Bed,
            bed.Label,
            bed.Status == BedStatus.Active,
            bed.BedSourceVersion,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<InventoryRoomTopologySnapshot?> GetRoomAsync(
        Guid propertyId,
        Guid roomId,
        CancellationToken cancellationToken) =>
        await dbContext.RoomTopology
            .AsNoTracking()
            .Where(room => room.Id == roomId && room.PropertyId == propertyId && room.IsKnown)
            .Select(room => new InventoryRoomTopologySnapshot(
                room.PropertyId,
                room.Id,
                room.Status,
                dbContext.BedTopology.Count(bed =>
                    bed.RoomId == room.Id && bed.IsKnown && bed.Status == BedStatus.Active)))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyCollection<InventoryUnitDefinitionSnapshot>> GetUnitDefinitionsAsync(
        Guid propertyId,
        Guid? roomId,
        Guid? inventoryUnitId,
        bool touchVersions,
        CancellationToken cancellationToken)
    {
        IQueryable<InventoryUnit> query = dbContext.InventoryUnits.Where(unit => unit.PropertyId == propertyId);
        if (roomId.HasValue)
        {
            query = query.Where(unit => unit.RoomId == roomId.Value);
        }

        if (inventoryUnitId.HasValue)
        {
            query = query.Where(unit => unit.Id == inventoryUnitId.Value);
        }

        List<InventoryUnit> units = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<InventoryUnit> localUnits = dbContext.InventoryUnits.Local.Where(unit =>
            unit.PropertyId == propertyId &&
            (!roomId.HasValue || unit.RoomId == roomId.Value) &&
            (!inventoryUnitId.HasValue || unit.Id == inventoryUnitId.Value));
        foreach (InventoryUnit local in localUnits)
        {
            if (units.All(unit => unit.Id != local.Id))
            {
                units.Add(local);
            }
        }

        InventoryPropertyTopology? property = dbContext.PropertyTopology.Local
            .FirstOrDefault(item => item.Id == propertyId) ??
            await dbContext.PropertyTopology.FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken)
                .ConfigureAwait(false);
        Guid[] roomIds = units.Select(unit => unit.RoomId).Distinct().ToArray();
        List<InventoryRoomTopology> rooms = roomIds.Length == 0
            ? []
            : await dbContext.RoomTopology.Where(item => roomIds.Contains(item.Id)).ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        foreach (InventoryRoomTopology local in dbContext.RoomTopology.Local.Where(item => roomIds.Contains(item.Id)))
        {
            if (rooms.All(item => item.Id != local.Id))
            {
                rooms.Add(local);
            }
        }

        List<RoomInventoryConfiguration> configurations = roomIds.Length == 0
            ? []
            : await dbContext.RoomConfigurations.Where(item => roomIds.Contains(item.Id)).ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        foreach (RoomInventoryConfiguration local in dbContext.RoomConfigurations.Local.Where(item => roomIds.Contains(item.Id)))
        {
            if (configurations.All(item => item.Id != local.Id))
            {
                configurations.Add(local);
            }
        }

        Dictionary<Guid, InventoryRoomTopology> roomsById = rooms.ToDictionary(item => item.Id);
        Dictionary<Guid, RoomInventoryConfiguration> configurationsByRoom = configurations.ToDictionary(item => item.Id);
        List<InventoryUnitDefinitionSnapshot> snapshots = [];
        foreach (InventoryUnit unit in units.Where(item => item.IsKnown).OrderBy(item => item.RoomId).ThenBy(item => item.Id))
        {
            if (touchVersions)
            {
                unit.TouchAvailability();
            }

            bool topologyActive = property is { IsKnown: true, Status: PropertyStatus.Active } &&
                                  roomsById.TryGetValue(unit.RoomId, out InventoryRoomTopology? room) &&
                                  room.IsKnown &&
                                  room.Status == RoomStatus.Active &&
                                  unit.IsTopologyActive;
            RoomInventoryConfiguration? configuration = configurationsByRoom.GetValueOrDefault(unit.RoomId);
            RoomSalesMode salesMode = configuration?.SalesMode ?? RoomSalesMode.Unconfigured;
            bool sellable = topologyActive &&
                            ((unit.Kind == InventoryUnitKind.Room && salesMode == RoomSalesMode.RoomLevel) ||
                             (unit.Kind == InventoryUnitKind.Bed && salesMode == RoomSalesMode.BedLevel));
            snapshots.Add(new(
                unit.ScopeId,
                unit.Id,
                unit.PropertyId,
                unit.RoomId,
                unit.BedId,
                unit.Kind,
                unit.Label,
                topologyActive,
                sellable,
                configuration?.Version ?? 1,
                unit.AvailabilityMutationVersion));
        }

        return snapshots;
    }

    private async Task EnsurePropertyPlaceholderAsync(
        string scopeId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        if (dbContext.PropertyTopology.Local.Any(item => item.Id == propertyId && item.ScopeId == scopeId) ||
            await dbContext.PropertyTopology.AnyAsync(item => item.Id == propertyId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        dbContext.PropertyTopology.Add(InventoryPropertyTopology.Create(propertyId, scopeId));
    }

    private async Task<InventoryRoomTopology> EnsureRoomPlaceholderAsync(
        string scopeId,
        Guid propertyId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        InventoryRoomTopology? room = dbContext.RoomTopology.Local.FirstOrDefault(
            item => item.Id == roomId && item.ScopeId == scopeId) ??
            await dbContext.RoomTopology.FirstOrDefaultAsync(item => item.Id == roomId, cancellationToken).ConfigureAwait(false);
        if (room is not null)
        {
            return room;
        }

        room = InventoryRoomTopology.Create(roomId, scopeId, propertyId);
        dbContext.RoomTopology.Add(room);
        return room;
    }

    private async Task ApplyInventoryUnitAsync(
        string scopeId,
        Guid propertyId,
        Guid roomId,
        Guid? bedId,
        InventoryUnitKind kind,
        string? label,
        bool isTopologyActive,
        long sourceVersion,
        CancellationToken cancellationToken)
    {
        Guid inventoryUnitId = bedId ?? roomId;
        InventoryUnit? unit = dbContext.InventoryUnits.Local.FirstOrDefault(
                item => item.Id == inventoryUnitId && item.ScopeId == scopeId) ??
            await dbContext.InventoryUnits.FirstOrDefaultAsync(
                item => item.Id == inventoryUnitId,
                cancellationToken).ConfigureAwait(false);
        if (unit is null)
        {
            unit = kind == InventoryUnitKind.Room
                ? InventoryUnit.CreateRoom(roomId, scopeId, propertyId)
                : InventoryUnit.CreateBed(inventoryUnitId, scopeId, propertyId, roomId);
            dbContext.InventoryUnits.Add(unit);
        }

        unit.Apply(
            propertyId,
            roomId,
            bedId,
            kind,
            label,
            isTopologyActive,
            sourceVersion);
    }
}
