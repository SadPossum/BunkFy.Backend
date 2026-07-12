namespace BunkFy.Modules.Properties.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class RoomStatusJsonConverter : JsonConverter<RoomStatus>
{
    public override RoomStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Room status must be a string.");
        }

        return RoomStatusNames.TryParse(reader.GetString(), out RoomStatus status)
            ? status
            : throw new JsonException("Room status is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        RoomStatus value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(RoomStatusNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Room status is invalid.", exception);
        }
    }
}
