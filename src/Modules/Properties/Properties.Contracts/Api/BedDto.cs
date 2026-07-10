namespace Properties.Contracts;

public sealed record BedDto(
    Guid BedId,
    Guid RoomId,
    Guid PropertyId,
    string Label,
    BedStatus Status,
    long Version,
    long RoomVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? RetiredAtUtc);
