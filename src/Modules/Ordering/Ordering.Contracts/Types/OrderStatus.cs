namespace Ordering.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrderStatusJsonConverter))]
public enum OrderStatus
{
    Unknown = 0,
    Submitted = 1
}
