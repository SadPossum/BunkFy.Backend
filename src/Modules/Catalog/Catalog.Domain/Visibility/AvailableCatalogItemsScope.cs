namespace Catalog.Domain.Visibility;

using Catalog.Domain.ValueObjects;

public sealed record AvailableCatalogItemsScope(string TenantId, CatalogRegionCode Region);
