namespace Properties.Application.Ports;

using Properties.Contracts;
using Gma.Framework.Pagination;

public interface IPropertiesReadRepository
{
    Task<PropertyDto?> GetPropertyAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<PropertyListResponse> ListPropertiesAsync(PageRequest pageRequest, CancellationToken cancellationToken);
    Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken cancellationToken);
    Task<RoomListResponse> ListRoomsAsync(Guid propertyId, PageRequest pageRequest, CancellationToken cancellationToken);
    Task<BedListResponse> ListBedsAsync(Guid roomId, PageRequest pageRequest, CancellationToken cancellationToken);
}
