namespace Ordering.Domain.Visibility;

using Ordering.Domain.ValueObjects;

public sealed record UserOrdersScope(string TenantId, OrderUserId UserId);
