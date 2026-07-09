namespace Properties.Persistence.Repositories;

using Properties.Application.Mapping;
using Properties.Application.Ports;
using Properties.Contracts;
using Properties.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Pagination;

internal sealed class PropertiesReadRepository(PropertiesDbContext dbContext) : IPropertiesReadRepository
{
    public async Task<PropertyDto?> GetPropertyAsync(Guid propertyId, CancellationToken cancellationToken)
    {
        Property? property = await dbContext.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(property => property.Id == propertyId, cancellationToken)
            .ConfigureAwait(false);

        return property is null ? null : PropertiesMapper.ToDto(property);
    }

    public async Task<PropertyListResponse> ListPropertiesAsync(PageRequest pageRequest, CancellationToken cancellationToken)
    {
        List<Property> properties = await dbContext.Properties
            .AsNoTracking()
            .OrderBy(property => property.Code)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PropertyListResponse(properties.Select(PropertiesMapper.ToDto).ToArray(), pageRequest.Page, pageRequest.PageSize);
    }

    public async Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken cancellationToken)
    {
        Room? room = await dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(room => room.Id == roomId, cancellationToken)
            .ConfigureAwait(false);

        return room is null ? null : PropertiesMapper.ToDto(room);
    }

    public async Task<RoomListResponse> ListRoomsAsync(Guid propertyId, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        List<Room> rooms = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.PropertyId == propertyId)
            .OrderBy(room => room.Name)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new RoomListResponse(rooms.Select(PropertiesMapper.ToDto).ToArray(), pageRequest.Page, pageRequest.PageSize);
    }

    public async Task<BedListResponse> ListBedsAsync(Guid roomId, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        List<BedDto> beds = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Id == roomId)
            .SelectMany(room => room.Beds)
            .OrderBy(bed => bed.Label.Value)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .Select(bed => new BedDto(
                bed.Id,
                bed.RoomId,
                bed.PropertyId,
                bed.Label.Value,
                PropertiesMapper.MapStatus(bed.Status),
                bed.CreatedAtUtc,
                bed.UpdatedAtUtc,
                bed.RetiredAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new BedListResponse(beds, pageRequest.Page, pageRequest.PageSize);
    }
}
