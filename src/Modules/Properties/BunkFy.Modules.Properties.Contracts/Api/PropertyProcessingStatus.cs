namespace BunkFy.Modules.Properties.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(PropertyProcessingStatusJsonConverter))]
public enum PropertyProcessingStatus
{
    Unknown = 0,
    Unconfigured = 1,
    Enabled = 2,
    Suspended = 3
}
