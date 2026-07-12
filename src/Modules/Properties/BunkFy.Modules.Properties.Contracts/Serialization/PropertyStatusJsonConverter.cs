namespace BunkFy.Modules.Properties.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class PropertyStatusJsonConverter : JsonConverter<PropertyStatus>
{
    public override PropertyStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Property status must be a string.");
        }

        return PropertyStatusNames.TryParse(reader.GetString(), out PropertyStatus status)
            ? status
            : throw new JsonException("Property status is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        PropertyStatus value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(PropertyStatusNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Property status is invalid.", exception);
        }
    }
}
