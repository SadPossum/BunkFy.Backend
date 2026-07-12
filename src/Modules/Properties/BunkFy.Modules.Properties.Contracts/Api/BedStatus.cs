namespace BunkFy.Modules.Properties.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(BedStatusJsonConverter))]
public enum BedStatus
{
    Unknown = 0,
    Active = 1,
    Retired = 2
}
