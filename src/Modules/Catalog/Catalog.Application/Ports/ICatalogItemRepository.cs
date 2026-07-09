namespace Catalog.Application.Ports;

using Catalog.Domain.Aggregates;

public interface ICatalogItemRepository
{
    Task AddAsync(CatalogItem item, CancellationToken cancellationToken);
    Task<CatalogItem?> GetAsync(Guid itemId, CancellationToken cancellationToken);
    Task<bool> SkuExistsAsync(string sku, Guid? excludingItemId, CancellationToken cancellationToken);
}
