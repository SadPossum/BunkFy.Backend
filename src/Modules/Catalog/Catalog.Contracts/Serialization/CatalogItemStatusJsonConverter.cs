namespace Catalog.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class CatalogItemStatusJsonConverter : JsonConverter<CatalogItemStatus>
{
    public override CatalogItemStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Catalog item status must be a string.");
        }

        return CatalogItemStatusNames.TryParse(reader.GetString(), out CatalogItemStatus status)
            ? status
            : throw new JsonException("Catalog item status is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        CatalogItemStatus value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(CatalogItemStatusNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Catalog item status is invalid.", exception);
        }
    }
}
