namespace BunkFy.Modules.Inventory.Application.Ports;

using Gma.Framework.Pagination;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;

public interface IManualInventoryBlockRepository
{
    Task AddAsync(ManualInventoryBlock block, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyCollection<ManualInventoryBlock> blocks, CancellationToken cancellationToken);
    Task<ManualInventoryBlock?> GetAsync(Guid propertyId, Guid blockId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ManualInventoryBlock>> GetActiveGroupAsync(
        Guid propertyId,
        Guid blockGroupId,
        CancellationToken cancellationToken);
    Task<bool> HasActiveOverlapAsync(
        Guid inventoryUnitId,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken);
    Task<bool> HasAnyActiveOverlapAsync(
        IReadOnlyCollection<Guid> inventoryUnitIds,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken);
    Task TouchUnitAsync(Guid inventoryUnitId, CancellationToken cancellationToken);
    Task TouchUnitsAsync(IReadOnlyCollection<Guid> inventoryUnitIds, CancellationToken cancellationToken);
    Task<ManualInventoryBlockListResponse> ListAsync(
        Guid propertyId,
        Guid? inventoryUnitId,
        bool includeReleased,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
