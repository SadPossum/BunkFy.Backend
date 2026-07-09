namespace Ordering.Application.Ports;

using Catalog.Contracts;
using Ordering.Domain.Aggregates;

public sealed record CatalogItemProjectionSnapshot
{
    public CatalogItemProjectionSnapshot(
        Guid catalogItemId,
        string sku,
        string name,
        decimal price,
        string currency,
        CatalogItemStatus status,
        IReadOnlyCollection<string>? availableRegions = null)
    {
        if (catalogItemId == Guid.Empty)
        {
            throw new ArgumentException("Catalog item id is required.", nameof(catalogItemId));
        }

        this.CatalogItemId = catalogItemId;
        this.Sku = Order.NormalizeCatalogSku(sku);
        this.Name = Order.NormalizeCatalogItemName(name);
        this.Price = price;
        this.Currency = Order.NormalizeCurrency(currency);
        this.Status = NormalizeStatus(status);
        this.AvailableRegions = CatalogRegionCodes.NormalizeMany(availableRegions);
    }

    public Guid CatalogItemId { get; }
    public string Sku { get; }
    public string Name { get; }
    public decimal Price { get; }
    public string Currency { get; }
    public CatalogItemStatus Status { get; }
    public IReadOnlyCollection<string> AvailableRegions { get; }

    public bool IsAvailableInRegion(string regionCode)
    {
        string normalizedRegion = CatalogRegionCodes.Normalize(regionCode);
        return this.AvailableRegions.Count == 0 ||
               this.AvailableRegions.Contains(normalizedRegion, StringComparer.Ordinal);
    }

    private static CatalogItemStatus NormalizeStatus(CatalogItemStatus status) =>
        Enum.IsDefined(status) ? status : CatalogItemStatus.Unknown;
}
