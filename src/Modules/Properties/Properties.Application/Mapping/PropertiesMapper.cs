namespace Properties.Application.Mapping;

using Properties.Contracts;
using Properties.Domain.Aggregates;
using Properties.Domain.Entities;

public static class PropertiesMapper
{
    public static PropertyDto ToDto(Property property) =>
        new(
            property.Id,
            property.Name.Value,
            property.Code.Value,
            property.TimeZoneId.Value,
            MapStatus(property.Status),
            property.Version,
            property.CreatedAtUtc,
            property.UpdatedAtUtc,
            property.RetiredAtUtc);

    public static RoomDto ToDto(Room room) =>
        new(
            room.Id,
            room.PropertyId,
            room.Name.Value,
            room.BuildingLabel?.Value,
            room.FloorLabel?.Value,
            MapStatus(room.Status),
            room.Version,
            room.CreatedAtUtc,
            room.UpdatedAtUtc,
            room.RetiredAtUtc);

    public static BedDto ToDto(Bed bed, long roomVersion) =>
        new(
            bed.Id,
            bed.RoomId,
            bed.PropertyId,
            bed.Label.Value,
            MapStatus(bed.Status),
            bed.Version,
            roomVersion,
            bed.CreatedAtUtc,
            bed.UpdatedAtUtc,
            bed.RetiredAtUtc);

    public static PropertyStatus MapStatus(PropertyState status) =>
        status switch
        {
            PropertyState.Active => PropertyStatus.Active,
            PropertyState.Retired => PropertyStatus.Retired,
            _ => PropertyStatus.Unknown
        };

    public static RoomStatus MapStatus(RoomState status) =>
        status switch
        {
            RoomState.Active => RoomStatus.Active,
            RoomState.Retired => RoomStatus.Retired,
            _ => RoomStatus.Unknown
        };

    public static BedStatus MapStatus(BedState status) =>
        status switch
        {
            BedState.Active => BedStatus.Active,
            BedState.Retired => BedStatus.Retired,
            _ => BedStatus.Unknown
        };
}
