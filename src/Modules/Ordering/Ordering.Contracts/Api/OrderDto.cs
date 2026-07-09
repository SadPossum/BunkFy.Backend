namespace Ordering.Contracts;

public sealed record OrderDto(
    Guid OrderId,
    string UserId,
    Guid CatalogItemId,
    string CatalogSku,
    string CatalogItemName,
    decimal UnitPrice,
    string Currency,
    string RegionCode,
    int Quantity,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset CreatedAtUtc);
