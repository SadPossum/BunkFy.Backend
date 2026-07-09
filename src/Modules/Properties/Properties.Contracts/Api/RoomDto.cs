namespace Properties.Contracts;

public sealed record RoomDto(
    Guid RoomId,
    Guid PropertyId,
    string Name,
    string? BuildingLabel,
    string? FloorLabel,
    RoomStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? RetiredAtUtc);
