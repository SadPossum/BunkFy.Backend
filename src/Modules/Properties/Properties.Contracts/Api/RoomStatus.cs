namespace Properties.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(RoomStatusJsonConverter))]
public enum RoomStatus
{
    Unknown = 0,
    Active = 1,
    Retired = 2
}
