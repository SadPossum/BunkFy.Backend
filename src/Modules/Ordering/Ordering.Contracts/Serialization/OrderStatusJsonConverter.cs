namespace Ordering.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class OrderStatusJsonConverter : JsonConverter<OrderStatus>
{
    public override OrderStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Order status must be a string.");
        }

        return OrderStatusNames.TryParse(reader.GetString(), out OrderStatus status)
            ? status
            : throw new JsonException("Order status is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        OrderStatus value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(OrderStatusNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Order status is invalid.", exception);
        }
    }
}
