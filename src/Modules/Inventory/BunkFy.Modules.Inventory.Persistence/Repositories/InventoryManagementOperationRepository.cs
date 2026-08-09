namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryManagementOperationRepository(
    InventoryDbContext dbContext)
    : IInventoryManagementOperationRepository
{
    public async Task<InventoryManagementOperationRecord?> GetAsync(
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        InventoryManagementOperation? operation = await dbContext
            .ManagementOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ResourceKind == resourceKind &&
                    item.ResourceId == resourceId &&
                    item.Id == operationId,
                cancellationToken).ConfigureAwait(false);
        return operation?.ToRecord();
    }

    public Task AddAsync(
        InventoryManagementOperationRecord operation,
        CancellationToken cancellationToken)
    {
        dbContext.ManagementOperations.Add(
            new InventoryManagementOperation(operation));
        return Task.CompletedTask;
    }
}
