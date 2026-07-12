namespace BunkFy.Modules.Properties.Contracts;

public static class PropertyStatusNames
{
    public static string ToWireName(PropertyStatus status) =>
        status switch
        {
            PropertyStatus.Active => "active",
            PropertyStatus.Retired => "retired",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Property status is invalid.")
        };

    public static bool TryParse(string? value, out PropertyStatus status)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Enum.TryParse(normalized, ignoreCase: true, out status) &&
            status is not PropertyStatus.Unknown &&
            Enum.IsDefined(status))
        {
            return true;
        }

        status = normalized switch
        {
            "active" => PropertyStatus.Active,
            "retired" => PropertyStatus.Retired,
            _ => PropertyStatus.Unknown
        };

        return status is not PropertyStatus.Unknown;
    }
}
