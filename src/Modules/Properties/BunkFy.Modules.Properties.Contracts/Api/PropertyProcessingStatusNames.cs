namespace BunkFy.Modules.Properties.Contracts;

public static class PropertyProcessingStatusNames
{
    public static string ToWireName(PropertyProcessingStatus status) =>
        status switch
        {
            PropertyProcessingStatus.Unconfigured => "unconfigured",
            PropertyProcessingStatus.Enabled => "enabled",
            PropertyProcessingStatus.Suspended => "suspended",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Property processing status is invalid.")
        };

    public static bool TryParse(string? value, out PropertyProcessingStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "unconfigured" => PropertyProcessingStatus.Unconfigured,
            "enabled" => PropertyProcessingStatus.Enabled,
            "suspended" => PropertyProcessingStatus.Suspended,
            _ => PropertyProcessingStatus.Unknown
        };

        return status is not PropertyProcessingStatus.Unknown;
    }
}
