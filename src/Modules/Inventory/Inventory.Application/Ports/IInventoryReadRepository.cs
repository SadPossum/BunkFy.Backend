namespace Inventory.Application.Ports;

using Gma.Framework.Pagination;
using Inventory.Contracts;

public interface IInventoryReadRepository
{
    Task<bool> PropertyExistsAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<RoomInventoryDto?> GetRoomAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken);
    Task<InventoryUnitSnapshot?> GetUnitAsync(Guid propertyId, Guid inventoryUnitId, CancellationToken cancellationToken);
    Task<RoomInventoryListResponse> ListRoomsAsync(Guid propertyId, PageRequest pageRequest, CancellationToken cancellationToken);
    Task<InventoryAvailabilityResponse> GetAvailabilityAsync(
        Guid propertyId,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken);
}
