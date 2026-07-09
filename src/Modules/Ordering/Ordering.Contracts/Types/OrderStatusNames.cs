namespace Ordering.Contracts;

public static class OrderStatusNames
{
    public static string ToWireName(OrderStatus status) =>
        status switch
        {
            OrderStatus.Submitted => "submitted",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Order status is invalid.")
        };

    public static bool TryParse(string? value, out OrderStatus status)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Enum.TryParse(normalized, ignoreCase: true, out status) &&
            status is not OrderStatus.Unknown &&
            Enum.IsDefined(status))
        {
            return true;
        }

        status = normalized switch
        {
            "submitted" => OrderStatus.Submitted,
            _ => OrderStatus.Unknown
        };

        return status is not OrderStatus.Unknown;
    }
}
