namespace Catalog.Contracts;

public static class CatalogItemStatusNames
{
    public static string ToWireName(CatalogItemStatus status) =>
        status switch
        {
            CatalogItemStatus.Active => "active",
            CatalogItemStatus.Discontinued => "discontinued",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Catalog item status is invalid.")
        };

    public static bool TryParse(string? value, out CatalogItemStatus status)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Enum.TryParse(normalized, ignoreCase: true, out status) &&
            status is not CatalogItemStatus.Unknown &&
            Enum.IsDefined(status))
        {
            return true;
        }

        status = normalized switch
        {
            "active" => CatalogItemStatus.Active,
            "discontinued" => CatalogItemStatus.Discontinued,
            _ => CatalogItemStatus.Unknown
        };

        return status is not CatalogItemStatus.Unknown;
    }
}
