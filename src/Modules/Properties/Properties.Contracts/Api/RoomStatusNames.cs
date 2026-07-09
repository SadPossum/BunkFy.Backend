namespace Properties.Contracts;

public static class RoomStatusNames
{
    public static string ToWireName(RoomStatus status) =>
        status switch
        {
            RoomStatus.Active => "active",
            RoomStatus.Retired => "retired",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Room status is invalid.")
        };

    public static bool TryParse(string? value, out RoomStatus status)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Enum.TryParse(normalized, ignoreCase: true, out status) &&
            status is not RoomStatus.Unknown &&
            Enum.IsDefined(status))
        {
            return true;
        }

        status = normalized switch
        {
            "active" => RoomStatus.Active,
            "retired" => RoomStatus.Retired,
            _ => RoomStatus.Unknown
        };

        return status is not RoomStatus.Unknown;
    }
}
