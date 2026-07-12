namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using System.Globalization;
using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Properties.Contracts;

internal sealed class InventoryAvailabilityProjectionExportSource(InventoryDbContext dbContext)
    : IInventoryAvailabilityProjectionExportSource
{
    public async Task<ProjectionReadBatch<InventoryAvailabilityProjectionExport>> ReadAsync(
        ProjectionRebuildRequest request,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long? normalizedCursor = NormalizeCursor(cursor);
        IQueryable<InventoryPropertyTopology> query = dbContext.PropertyTopology.AsNoTracking();
        if (normalizedCursor.HasValue)
        {
            query = query.Where(property => property.ProjectionOrdinal > normalizedCursor.Value);
        }

        List<InventoryPropertyTopology> rows = await query
            .Where(property => property.IsKnown)
            .OrderBy(property => property.ProjectionOrdinal)
            .Take(request.BatchSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        bool hasMore = rows.Count > request.BatchSize;
        InventoryPropertyTopology[] page = rows.Take(request.BatchSize).ToArray();
        Guid[] propertyIds = page.Select(property => property.Id).ToArray();

        List<InventoryRoomTopology> rooms = propertyIds.Length == 0
            ? []
            : await dbContext.RoomTopology
                .AsNoTracking()
                .Where(room => propertyIds.Contains(room.PropertyId) && room.IsKnown)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        List<InventoryUnit> units = propertyIds.Length == 0
            ? []
            : await dbContext.InventoryUnits
                .AsNoTracking()
                .Where(unit => propertyIds.Contains(unit.PropertyId) && unit.IsKnown)
                .OrderBy(unit => unit.RoomId)
                .ThenBy(unit => unit.Kind)
                .ThenBy(unit => unit.Label)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        Guid[] roomIds = units.Select(unit => unit.RoomId).Distinct().ToArray();
        Dictionary<Guid, RoomInventoryConfiguration> configurations = roomIds.Length == 0
            ? []
            : await dbContext.RoomConfigurations
                .AsNoTracking()
                .Where(configuration => roomIds.Contains(configuration.Id))
                .ToDictionaryAsync(configuration => configuration.Id, cancellationToken)
                .ConfigureAwait(false);
        Guid[] unitIds = units.Select(unit => unit.Id).ToArray();
        List<ManualInventoryBlock> blocks = unitIds.Length == 0
            ? []
            : await dbContext.ManualBlocks
                .AsNoTracking()
                .Where(block => unitIds.Contains(block.InventoryUnitId))
                .OrderBy(block => block.Arrival)
                .ThenBy(block => block.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        List<InventoryAllocation> allocations = propertyIds.Length == 0
            ? []
            : await dbContext.Allocations
                .AsNoTracking()
                .Include(allocation => allocation.Units)
                .Where(allocation => propertyIds.Contains(allocation.PropertyId))
                .OrderBy(allocation => allocation.CreatedAtUtc)
                .ThenBy(allocation => allocation.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        ILookup<Guid, InventoryUnit> unitsByProperty = units.ToLookup(unit => unit.PropertyId);
        Dictionary<Guid, InventoryRoomTopology> roomsById = rooms.ToDictionary(room => room.Id);
        ILookup<Guid, ManualInventoryBlock> blocksByUnit = blocks.ToLookup(block => block.InventoryUnitId);
        ILookup<Guid, InventoryAllocation> allocationsByUnit = allocations
            .SelectMany(allocation => allocation.Units.Select(unit => new { unit.InventoryUnitId, Allocation = allocation }))
            .ToLookup(item => item.InventoryUnitId, item => item.Allocation);
        InventoryAvailabilityProjectionExport[] snapshots = page
            .Select(property => new InventoryAvailabilityProjectionExport(
                property.ScopeId,
                property.Id,
                unitsByProperty[property.Id]
                    .Select(unit => MapUnit(property, unit, roomsById, configurations, blocksByUnit, allocationsByUnit))
                    .ToArray()))
            .ToArray();
        string? nextCursor = page.Length == 0
            ? null
            : page[^1].ProjectionOrdinal.ToString(CultureInfo.InvariantCulture);
        return new(snapshots, nextCursor, hasMore);
    }

    private static InventoryUnitProjectionExport MapUnit(
        InventoryPropertyTopology property,
        InventoryUnit unit,
        Dictionary<Guid, InventoryRoomTopology> rooms,
        Dictionary<Guid, RoomInventoryConfiguration> configurations,
        ILookup<Guid, ManualInventoryBlock> blocksByUnit,
        ILookup<Guid, InventoryAllocation> allocationsByUnit)
    {
        bool roomActive = rooms.TryGetValue(unit.RoomId, out InventoryRoomTopology? room) &&
                          property.Status == PropertyStatus.Active &&
                          room.Status == RoomStatus.Active;
        RoomSalesMode salesMode = configurations.GetValueOrDefault(unit.RoomId)?.SalesMode ?? RoomSalesMode.Unconfigured;
        bool topologyActive = roomActive && unit.IsTopologyActive;
        bool sellable = topologyActive &&
                        ((unit.Kind == InventoryUnitKind.Room && salesMode == RoomSalesMode.RoomLevel) ||
                         (unit.Kind == InventoryUnitKind.Bed && salesMode == RoomSalesMode.BedLevel));
        return new(
            unit.Id,
            unit.RoomId,
            unit.BedId,
            unit.Kind,
            unit.Label,
            topologyActive,
            sellable,
            configurations.GetValueOrDefault(unit.RoomId)?.Version ?? 1,
            unit.AvailabilityMutationVersion,
            blocksByUnit[unit.Id]
                .Select(block => new ManualInventoryBlockProjectionExport(
                    block.Id,
                    block.Arrival,
                    block.Departure,
                    block.Status == ManualInventoryBlockState.Active
                        ? ManualInventoryBlockStatus.Active
                        : ManualInventoryBlockStatus.Released,
                    block.Version))
                .ToArray(),
            allocationsByUnit[unit.Id]
                .Select(allocation => new InventoryAllocationProjectionExport(
                    allocation.Id,
                    allocation.ReservationId,
                    allocation.Arrival,
                    allocation.Departure,
                    allocation.Status switch
                    {
                        InventoryAllocationState.Active => InventoryAllocationStatus.Active,
                        InventoryAllocationState.Rejected => InventoryAllocationStatus.Rejected,
                        InventoryAllocationState.Released => InventoryAllocationStatus.Released,
                        _ => InventoryAllocationStatus.Unknown
                    },
                    allocation.Version))
                .ToArray());
    }

    private static long? NormalizeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        return long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out long ordinal) && ordinal > 0
            ? ordinal
            : throw new ArgumentException("Projection export cursor must be a positive property ordinal.", nameof(cursor));
    }
}
