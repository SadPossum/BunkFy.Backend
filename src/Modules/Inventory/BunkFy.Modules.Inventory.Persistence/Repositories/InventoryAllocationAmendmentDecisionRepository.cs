namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryAllocationAmendmentDecisionRepository(InventoryDbContext dbContext)
    : IInventoryAllocationAmendmentDecisionRepository
{
    public async Task<InventoryAllocationAmendmentDecisionRecord?> GetAsync(
        Guid amendmentRequestId,
        CancellationToken cancellationToken)
    {
        InventoryAllocationAmendmentDecision? decision = await dbContext.AllocationAmendmentDecisions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == amendmentRequestId, cancellationToken)
            .ConfigureAwait(false);
        return decision?.ToRecord();
    }

    public Task AddAsync(
        InventoryAllocationAmendmentDecisionRecord decision,
        CancellationToken cancellationToken)
    {
        dbContext.AllocationAmendmentDecisions.Add(new InventoryAllocationAmendmentDecision(decision));
        return Task.CompletedTask;
    }
}
