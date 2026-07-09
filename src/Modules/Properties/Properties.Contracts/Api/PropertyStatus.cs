namespace Properties.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(PropertyStatusJsonConverter))]
public enum PropertyStatus
{
    Unknown = 0,
    Active = 1
}
