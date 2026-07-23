namespace BunkFy.Modules.Properties.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class PropertyProcessingEffectiveStatusJsonConverter : JsonConverter<PropertyProcessingEffectiveStatus>
{
    public override PropertyProcessingEffectiveStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Effective property processing status must be a string.");
        }

        return PropertyProcessingEffectiveStatusNames.TryParse(
            reader.GetString(),
            out PropertyProcessingEffectiveStatus status)
            ? status
            : throw new JsonException("Effective property processing status is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        PropertyProcessingEffectiveStatus value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(PropertyProcessingEffectiveStatusNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Effective property processing status is invalid.", exception);
        }
    }
}
