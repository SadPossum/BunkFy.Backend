namespace Catalog.Application.Mapping;

using Catalog.Contracts;
using Catalog.Domain.Aggregates;

internal static class CatalogItemMapper
{
    public static CatalogItemDto ToDto(CatalogItem item) =>
        new(
            item.Id,
            item.Sku.Value,
            item.Name.Value,
            item.Price.Value,
            item.Currency.Value,
            ToContractStatus(item.Status),
            item.AvailableRegions
                .Select(region => region.Region.Value)
                .Order(StringComparer.Ordinal)
                .ToArray());

    public static CatalogItemStatus ToContractStatus(CatalogItemState status) =>
        status switch
        {
            CatalogItemState.Active => CatalogItemStatus.Active,
            CatalogItemState.Discontinued => CatalogItemStatus.Discontinued,
            _ => CatalogItemStatus.Unknown
        };
}
