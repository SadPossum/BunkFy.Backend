namespace Catalog.Contracts;

public sealed record CatalogItemDto(
    Guid ItemId,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    CatalogItemStatus Status,
    IReadOnlyCollection<string> AvailableRegions);
