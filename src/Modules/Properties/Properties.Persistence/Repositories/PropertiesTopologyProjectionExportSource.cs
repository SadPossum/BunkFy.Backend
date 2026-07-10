namespace Properties.Persistence.Repositories;

using System.Globalization;
using Properties.Contracts;
using Properties.Domain.Aggregates;
using Properties.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.ProjectionRebuild;

internal sealed class PropertiesTopologyProjectionExportSource(PropertiesDbContext dbContext) : IPropertiesTopologyProjectionExportSource
{
    public async Task<ProjectionReadBatch<PropertyTopologyProjectionExport>> ReadAsync(
        ProjectionRebuildRequest request,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long? normalizedCursor = NormalizeCursor(cursor);
        IQueryable<Property> query = dbContext.Properties.AsNoTracking();
        if (normalizedCursor.HasValue)
        {
            query = query.Where(property => property.ProjectionOrdinal > normalizedCursor.Value);
        }

        List<Property> rows = await query
            .OrderBy(property => property.ProjectionOrdinal)
            .Take(request.BatchSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMore = rows.Count > request.BatchSize;
        Property[] page = rows.Take(request.BatchSize).ToArray();
        Guid[] propertyIds = page.Select(property => property.Id).ToArray();

        List<Room> rooms = propertyIds.Length == 0
            ? []
            : await dbContext.Rooms
                .AsNoTracking()
                .Include(room => room.Beds)
                .Where(room => propertyIds.Contains(room.PropertyId))
                .OrderBy(room => room.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        Dictionary<Guid, IReadOnlyList<Room>> roomsByProperty = rooms
            .GroupBy(room => room.PropertyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Room>)group.ToArray());

        string? nextCursor = page.Length == 0
            ? null
            : page[^1].ProjectionOrdinal.ToString(CultureInfo.InvariantCulture);

        return new ProjectionReadBatch<PropertyTopologyProjectionExport>(
            page.Select(property => Map(property, roomsByProperty)).ToArray(),
            nextCursor,
            hasMore);
    }

    private static PropertyTopologyProjectionExport Map(
        Property property,
        Dictionary<Guid, IReadOnlyList<Room>> roomsByProperty)
    {
        roomsByProperty.TryGetValue(property.Id, out IReadOnlyList<Room>? rooms);

        return new PropertyTopologyProjectionExport(
            property.ScopeId,
            property.Id,
            property.Name.Value,
            property.Code.Value,
            property.TimeZoneId.Value,
            MapStatus(property.Status),
            property.Version,
            (rooms ?? []).Select(MapRoom).ToArray());
    }

    private static RoomTopologyProjectionExport MapRoom(Room room) =>
        new(
            room.PropertyId,
            room.Id,
            room.Name.Value,
            room.BuildingLabel?.Value,
            room.FloorLabel?.Value,
            MapStatus(room.Status),
            room.Version,
            room.Beds
                .OrderBy(bed => bed.Label.Value, StringComparer.Ordinal)
                .Select(MapBed)
                .ToArray());

    private static BedTopologyProjectionExport MapBed(Bed bed) =>
        new(
            bed.PropertyId,
            bed.RoomId,
            bed.Id,
            bed.Label.Value,
            MapStatus(bed.Status),
            bed.Version);

    private static PropertyStatus MapStatus(PropertyState status) =>
        status switch
        {
            PropertyState.Active => PropertyStatus.Active,
            PropertyState.Retired => PropertyStatus.Retired,
            _ => PropertyStatus.Unknown
        };

    private static RoomStatus MapStatus(RoomState status) =>
        status switch
        {
            RoomState.Active => RoomStatus.Active,
            RoomState.Retired => RoomStatus.Retired,
            _ => RoomStatus.Unknown
        };

    private static BedStatus MapStatus(BedState status) =>
        status switch
        {
            BedState.Active => BedStatus.Active,
            BedState.Retired => BedStatus.Retired,
            _ => BedStatus.Unknown
        };

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
