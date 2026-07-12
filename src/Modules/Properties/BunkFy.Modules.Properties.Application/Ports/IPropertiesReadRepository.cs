namespace BunkFy.Modules.Properties.Application.Ports;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Pagination;

public interface IPropertiesReadRepository
{
    Task<PropertyDto?> GetPropertyAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<PropertyListResponse> ListPropertiesAsync(PageRequest pageRequest, CancellationToken cancellationToken);
    Task<PropertyListResponse> ListVisiblePropertiesAsync(
        PageRequest pageRequest,
        PropertiesVisibilityScope visibility,
        CancellationToken cancellationToken);
    Task<RoomDto?> GetRoomAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken);
    Task<RoomListResponse> ListRoomsAsync(Guid propertyId, PageRequest pageRequest, CancellationToken cancellationToken);
    Task<BedListResponse> ListBedsAsync(Guid propertyId, Guid roomId, PageRequest pageRequest, CancellationToken cancellationToken);
}
