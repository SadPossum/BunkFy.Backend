namespace BunkFy.Modules.Properties.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(PropertyProcessingEffectiveStatusJsonConverter))]
public enum PropertyProcessingEffectiveStatus
{
    Unknown = 0,
    Unconfigured = 1,
    Enabled = 2,
    Suspended = 3,
    Expired = 4,
    Revoked = 5
}

public static class PropertyProcessingEffectiveStatusNames
{
    public static string ToWireName(PropertyProcessingEffectiveStatus status) =>
        status switch
        {
            PropertyProcessingEffectiveStatus.Unconfigured => "unconfigured",
            PropertyProcessingEffectiveStatus.Enabled => "enabled",
            PropertyProcessingEffectiveStatus.Suspended => "suspended",
            PropertyProcessingEffectiveStatus.Expired => "expired",
            PropertyProcessingEffectiveStatus.Revoked => "revoked",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Effective property processing status is invalid.")
        };

    public static bool TryParse(string? value, out PropertyProcessingEffectiveStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "unconfigured" => PropertyProcessingEffectiveStatus.Unconfigured,
            "enabled" => PropertyProcessingEffectiveStatus.Enabled,
            "suspended" => PropertyProcessingEffectiveStatus.Suspended,
            "expired" => PropertyProcessingEffectiveStatus.Expired,
            "revoked" => PropertyProcessingEffectiveStatus.Revoked,
            _ => PropertyProcessingEffectiveStatus.Unknown
        };

        return status is not PropertyProcessingEffectiveStatus.Unknown;
    }
}
