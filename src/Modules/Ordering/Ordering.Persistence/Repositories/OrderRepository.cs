namespace Ordering.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Ordering.Application.Ports;
using Ordering.Domain.Aggregates;

internal sealed class OrderRepository(OrderingDbContext dbContext) : IOrderRepository
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        await dbContext.Orders.AddAsync(order, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> ListDistinctUserIdsByCatalogItemAsync(
        string tenantId,
        Guid catalogItemId,
        CancellationToken cancellationToken) =>
        await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.TenantId == tenantId && order.CatalogItemId == catalogItemId)
            .Select(order => order.UserId)
            .Distinct()
            .OrderBy(userId => userId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
}
