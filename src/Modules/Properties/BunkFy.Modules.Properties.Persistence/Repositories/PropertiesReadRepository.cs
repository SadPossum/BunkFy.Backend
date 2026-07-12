namespace BunkFy.Modules.Properties.Persistence.Repositories;

using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
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
        => await this.ListVisiblePropertiesAsync(
            pageRequest,
            PropertiesVisibilityScope.All,
            cancellationToken).ConfigureAwait(false);

    public async Task<PropertyListResponse> ListVisiblePropertiesAsync(
        PageRequest pageRequest,
        PropertiesVisibilityScope visibility,
        CancellationToken cancellationToken)
    {
        IQueryable<Property> query = dbContext.Properties.AsNoTracking();
        if (!visibility.IncludesAllProperties)
        {
            query = query.Where(property => visibility.PropertyIds.Contains(property.Id));
        }

        List<Property> properties = await query
            .OrderBy(property => property.Code)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PropertyListResponse(properties.Select(PropertiesMapper.ToDto).ToArray(), pageRequest.Page, pageRequest.PageSize);
    }

    public async Task<RoomDto?> GetRoomAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken)
    {
        Room? room = await dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(room => room.PropertyId == propertyId && room.Id == roomId, cancellationToken)
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

    public async Task<BedListResponse> ListBedsAsync(
        Guid propertyId,
        Guid roomId,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.PropertyId == propertyId && room.Id == roomId)
            .SelectMany(room => room.Beds.Select(bed => new { Bed = bed, RoomVersion = room.Version }))
            .OrderBy(item => item.Bed.Label)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<BedDto> beds = rows
            .Select(item => new BedDto(
                item.Bed.Id,
                item.Bed.RoomId,
                item.Bed.PropertyId,
                item.Bed.Label.Value,
                PropertiesMapper.MapStatus(item.Bed.Status),
                item.Bed.Version,
                item.RoomVersion,
                item.Bed.CreatedAtUtc,
                item.Bed.UpdatedAtUtc,
                item.Bed.RetiredAtUtc))
            .ToList();

        return new BedListResponse(beds, pageRequest.Page, pageRequest.PageSize);
    }
}
