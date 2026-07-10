namespace Properties.Persistence.Repositories;

using Properties.Application.Ports;
using Properties.Domain.Aggregates;
using Properties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

internal sealed class RoomRepository(PropertiesDbContext dbContext) : IRoomRepository
{
    public async Task AddAsync(Room room, CancellationToken cancellationToken)
    {
        await dbContext.Rooms.AddAsync(room, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Room?> GetAsync(Guid roomId, CancellationToken cancellationToken) =>
        await dbContext.Rooms
            .Include(room => room.Beds)
            .FirstOrDefaultAsync(room => room.Id == roomId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> HasActiveRoomsAsync(Guid propertyId, CancellationToken cancellationToken) =>
        await dbContext.Rooms
            .AnyAsync(
                room => room.PropertyId == propertyId && room.Status == RoomState.Active,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> RoomNameExistsAsync(
        Guid propertyId,
        string name,
        Guid? excludingRoomId,
        CancellationToken cancellationToken)
    {
        RoomName normalized = RoomName.Create(name).Value;
        return await dbContext.Rooms
            .AnyAsync(
                room => room.PropertyId == propertyId &&
                        room.Name == normalized &&
                        (excludingRoomId == null || room.Id != excludingRoomId.Value),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
