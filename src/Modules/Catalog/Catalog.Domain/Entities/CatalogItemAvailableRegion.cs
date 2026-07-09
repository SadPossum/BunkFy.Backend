namespace Catalog.Domain.Entities;

using Catalog.Domain.ValueObjects;

public sealed class CatalogItemAvailableRegion
{
    private CatalogItemAvailableRegion() { }

    private CatalogItemAvailableRegion(CatalogRegionCode region) => this.Region = region;

    public CatalogRegionCode Region { get; private set; }

    public static CatalogItemAvailableRegion Create(CatalogRegionCode region) => new(region);
}
