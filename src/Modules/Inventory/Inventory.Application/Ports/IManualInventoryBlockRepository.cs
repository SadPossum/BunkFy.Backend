namespace Inventory.Application.Ports;

using Gma.Framework.Pagination;
using Inventory.Contracts;
using Inventory.Domain.Aggregates;

public interface IManualInventoryBlockRepository
{
    Task AddAsync(ManualInventoryBlock block, CancellationToken cancellationToken);
    Task<ManualInventoryBlock?> GetAsync(Guid propertyId, Guid blockId, CancellationToken cancellationToken);
    Task<bool> HasActiveOverlapAsync(
        Guid inventoryUnitId,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken);
    Task TouchUnitAsync(Guid inventoryUnitId, CancellationToken cancellationToken);
    Task<ManualInventoryBlockListResponse> ListAsync(
        Guid propertyId,
        Guid? inventoryUnitId,
        bool includeReleased,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
