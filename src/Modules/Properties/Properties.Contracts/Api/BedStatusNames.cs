namespace Properties.Contracts;

public static class BedStatusNames
{
    public static string ToWireName(BedStatus status) =>
        status switch
        {
            BedStatus.Active => "active",
            BedStatus.Retired => "retired",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Bed status is invalid.")
        };

    public static bool TryParse(string? value, out BedStatus status)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Enum.TryParse(normalized, ignoreCase: true, out status) &&
            status is not BedStatus.Unknown &&
            Enum.IsDefined(status))
        {
            return true;
        }

        status = normalized switch
        {
            "active" => BedStatus.Active,
            "retired" => BedStatus.Retired,
            _ => BedStatus.Unknown
        };

        return status is not BedStatus.Unknown;
    }
}
