namespace BunkFy.Modules.Properties.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class PropertyProcessingStatusJsonConverter : JsonConverter<PropertyProcessingStatus>
{
    public override PropertyProcessingStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Property processing status must be a string.");
        }

        return PropertyProcessingStatusNames.TryParse(reader.GetString(), out PropertyProcessingStatus status)
            ? status
            : throw new JsonException("Property processing status is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        PropertyProcessingStatus value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(PropertyProcessingStatusNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Property processing status is invalid.", exception);
        }
    }
}
