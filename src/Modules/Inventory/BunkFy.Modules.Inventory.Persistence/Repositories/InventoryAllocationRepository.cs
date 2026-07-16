namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryAllocationRepository(InventoryDbContext dbContext)
    : IInventoryAllocationRepository
{
    public Task<InventoryAllocation?> GetByRequestAsync(
        Guid allocationRequestId,
        CancellationToken cancellationToken) =>
        dbContext.Allocations
            .Include(allocation => allocation.Units)
            .FirstOrDefaultAsync(allocation => allocation.AllocationRequestId == allocationRequestId, cancellationToken);

    public Task<InventoryAllocation?> GetByReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.Allocations
            .Include(allocation => allocation.Units)
            .FirstOrDefaultAsync(allocation => allocation.ReservationId == reservationId, cancellationToken);

    public Task<InventoryAllocation?> GetAsync(Guid allocationId, CancellationToken cancellationToken) =>
        dbContext.Allocations
            .Include(allocation => allocation.Units)
            .FirstOrDefaultAsync(allocation => allocation.Id == allocationId, cancellationToken);

    public Task AddAsync(InventoryAllocation allocation, CancellationToken cancellationToken)
    {
        dbContext.Allocations.Add(allocation);
        return Task.CompletedTask;
    }

}
