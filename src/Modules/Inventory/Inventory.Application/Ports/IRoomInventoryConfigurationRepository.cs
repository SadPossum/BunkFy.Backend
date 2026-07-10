namespace Inventory.Application.Ports;

using Inventory.Domain.Aggregates;

public interface IRoomInventoryConfigurationRepository
{
    Task EnsureAsync(string scopeId, Guid propertyId, Guid roomId, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task<RoomInventoryConfiguration?> GetAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken);
}
