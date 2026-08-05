namespace BunkFy.Modules.Properties.Contracts;

public sealed record BedListItemDto(
    Guid BedId,
    Guid RoomId,
    Guid PropertyId,
    string Label,
    BedStatus Status,
    long Version,
    long RoomVersion);
