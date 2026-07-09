namespace Ordering.Persistence.QueryScopes;

using Ordering.Domain.Aggregates;
using Ordering.Domain.Visibility;

internal static class OrderAccessScopeExtensions
{
    public static IQueryable<Order> ApplyUserOrdersScope(this IQueryable<Order> query, UserOrdersScope scope)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(scope);

        return query.Where(order =>
            order.TenantId == scope.TenantId &&
            order.UserId == scope.UserId.Value);
    }
}
