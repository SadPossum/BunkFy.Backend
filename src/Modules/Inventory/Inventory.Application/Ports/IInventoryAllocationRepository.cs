namespace Inventory.Application.Ports;

using Inventory.Domain.Aggregates;

public interface IInventoryAllocationRepository
{
    Task<InventoryAllocation?> GetByRequestAsync(Guid allocationRequestId, CancellationToken cancellationToken);
    Task<InventoryAllocation?> GetByReservationAsync(Guid reservationId, CancellationToken cancellationToken);
    Task<InventoryAllocation?> GetAsync(Guid allocationId, CancellationToken cancellationToken);
    Task AddAsync(InventoryAllocation allocation, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<InventoryAllocationUnitSnapshot>> GetUnitsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken);
    Task<bool> HasManualBlockConflictAsync(
        IReadOnlyCollection<Guid> inventoryUnitIds,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken);
    Task<bool> HasActiveAllocationConflictAsync(
        IReadOnlyCollection<Guid> inventoryUnitIds,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken);
    Task TouchUnitsAsync(IReadOnlyCollection<Guid> inventoryUnitIds, CancellationToken cancellationToken);
}
