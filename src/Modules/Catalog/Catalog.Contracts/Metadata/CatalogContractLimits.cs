namespace Catalog.Contracts;

public static class CatalogContractLimits
{
    public const int SkuMaxLength = 64;
    public const int NameMaxLength = 256;
    public const int CurrencyLength = 3;
    public const int PricePrecision = 18;
    public const int PriceScale = 2;
    public const int RegionCodeMaxLength = 32;
    public const int AvailableRegionMaxCount = 32;
}
