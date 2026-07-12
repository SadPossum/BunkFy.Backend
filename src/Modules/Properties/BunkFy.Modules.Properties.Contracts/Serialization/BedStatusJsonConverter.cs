namespace BunkFy.Modules.Properties.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class BedStatusJsonConverter : JsonConverter<BedStatus>
{
    public override BedStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Bed status must be a string.");
        }

        return BedStatusNames.TryParse(reader.GetString(), out BedStatus status)
            ? status
            : throw new JsonException("Bed status is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        BedStatus value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(BedStatusNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Bed status is invalid.", exception);
        }
    }
}
