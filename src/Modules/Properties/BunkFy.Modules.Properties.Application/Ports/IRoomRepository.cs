namespace BunkFy.Modules.Properties.Application.Ports;

using BunkFy.Modules.Properties.Domain.Aggregates;

public interface IRoomRepository
{
    Task AddAsync(Room room, CancellationToken cancellationToken);
    Task<Room?> GetAsync(Guid roomId, CancellationToken cancellationToken);
    Task<bool> HasActiveRoomsAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<bool> RoomNameExistsAsync(Guid propertyId, string name, Guid? excludingRoomId, CancellationToken cancellationToken);
}
