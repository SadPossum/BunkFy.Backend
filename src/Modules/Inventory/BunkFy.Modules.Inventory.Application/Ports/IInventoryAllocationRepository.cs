namespace BunkFy.Modules.Inventory.Application.Ports;

using BunkFy.Modules.Inventory.Domain.Aggregates;

public interface IInventoryAllocationRepository
{
    Task<InventoryAllocation?> GetByRequestAsync(Guid allocationRequestId, CancellationToken cancellationToken);
    Task<InventoryAllocation?> GetByReservationAsync(Guid reservationId, CancellationToken cancellationToken);
    Task<InventoryAllocation?> GetAsync(Guid allocationId, CancellationToken cancellationToken);
    Task AddAsync(InventoryAllocation allocation, CancellationToken cancellationToken);
}
