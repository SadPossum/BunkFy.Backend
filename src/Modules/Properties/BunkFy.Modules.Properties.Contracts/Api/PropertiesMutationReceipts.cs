namespace BunkFy.Modules.Properties.Contracts;

public sealed record PropertyMutationReceiptDto(
    Guid PropertyId,
    PropertyStatus Status,
    PropertyProcessingStatus ProcessingStatus,
    long Version);

public sealed record RoomMutationReceiptDto(
    Guid PropertyId,
    Guid RoomId,
    RoomStatus Status,
    long Version);

public sealed record BedMutationReceiptDto(
    Guid PropertyId,
    Guid RoomId,
    Guid BedId,
    BedStatus Status,
    long Version,
    long RoomVersion);

public sealed record BedBatchMutationReceiptDto(
    Guid PropertyId,
    Guid RoomId,
    int AffectedBedCount,
    long RoomVersion);
