namespace Catalog.Contracts;

public sealed record CatalogItemListResponse(
    IReadOnlyCollection<CatalogItemDto> Items,
    int Page,
    int PageSize);
