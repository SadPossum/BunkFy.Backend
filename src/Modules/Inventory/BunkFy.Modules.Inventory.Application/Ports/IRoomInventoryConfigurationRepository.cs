namespace BunkFy.Modules.Inventory.Application.Ports;

using BunkFy.Modules.Inventory.Domain.Aggregates;

public interface IRoomInventoryConfigurationRepository
{
    Task EnsureAsync(string scopeId, Guid propertyId, Guid roomId, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task<RoomInventoryConfiguration?> GetAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken);
}
