namespace Properties.Persistence.Repositories;

using Properties.Contracts;
using Properties.Domain.Aggregates;
using Properties.Domain.Entities;
using Properties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.Results;

internal sealed class PropertiesTopologyProjectionExportSource(PropertiesDbContext dbContext) : IPropertiesTopologyProjectionExportSource
{
    public async Task<ProjectionReadBatch<PropertyTopologyProjectionExport>> ReadAsync(
        ProjectionRebuildRequest request,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? normalizedCursor = NormalizeCursor(cursor);
        IQueryable<Property> query = dbContext.Properties.AsNoTracking();
        if (normalizedCursor is not null)
        {
            query = this.ApplyCursor(query, normalizedCursor);
        }

        List<Property> rows = await query
            .OrderBy(property => property.Code)
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

        string? nextCursor = page.Length == 0 ? null : page[^1].Code.Value;

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
            property.TenantId,
            property.Id,
            property.Name.Value,
            property.Code.Value,
            property.TimeZoneId.Value,
            MapStatus(property.Status),
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
            MapStatus(bed.Status));

    private static PropertyStatus MapStatus(PropertyState status) =>
        status switch
        {
            PropertyState.Active => PropertyStatus.Active,
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

    private IQueryable<Property> ApplyCursor(IQueryable<Property> query, string normalizedCursor)
    {
        if (dbContext.Database.IsNpgsql())
        {
            return dbContext.Properties
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "properties"."properties"
                    WHERE "Code" > {normalizedCursor}
                    """)
                .AsNoTracking();
        }

#pragma warning disable CA1309
        return query.Where(property => string.Compare(property.Code.Value, normalizedCursor) > 0);
#pragma warning restore CA1309
    }

    private static string? NormalizeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        Result<PropertyCode> result = PropertyCode.Create(cursor);
        return result.IsSuccess
            ? result.Value.Value
            : throw new ArgumentException("Projection export cursor must be a valid property code.", nameof(cursor));
    }
}
