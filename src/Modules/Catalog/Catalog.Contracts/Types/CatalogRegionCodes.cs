namespace Catalog.Contracts;

public static class CatalogRegionCodes
{
    public static string Normalize(string? regionCode, string? parameterName = null)
    {
        if (TryNormalize(regionCode, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(
            $"Region code must be {CatalogContractLimits.RegionCodeMaxLength} characters or fewer and contain only letters, digits, or hyphens.",
            parameterName ?? nameof(regionCode));
    }

    public static bool TryNormalize(string? regionCode, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return false;
        }

        string candidate = regionCode.Trim().ToUpperInvariant();
        if (candidate.Length > CatalogContractLimits.RegionCodeMaxLength ||
            candidate.StartsWith('-') ||
            candidate.EndsWith('-') ||
            candidate.Any(character => !IsAllowedRegionCodeCharacter(character)))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static IReadOnlyList<string> NormalizeMany(IEnumerable<string>? regionCodes)
    {
        if (regionCodes is null)
        {
            return [];
        }

        string[] normalized = regionCodes
            .Where(regionCode => !string.IsNullOrWhiteSpace(regionCode))
            .Select(regionCode => Normalize(regionCode))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return normalized.Length <= CatalogContractLimits.AvailableRegionMaxCount
            ? normalized
            : throw new ArgumentException(
                $"At most {CatalogContractLimits.AvailableRegionMaxCount} available regions can be supplied.",
                nameof(regionCodes));
    }

    private static bool IsAllowedRegionCodeCharacter(char character) =>
        character switch
        {
            >= 'A' and <= 'Z' => true,
            >= '0' and <= '9' => true,
            '-' => true,
            _ => false
        };
}
