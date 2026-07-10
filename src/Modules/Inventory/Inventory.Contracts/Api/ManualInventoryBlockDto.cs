namespace Inventory.Contracts;

public sealed record ManualInventoryBlockDto(
    Guid BlockId,
    Guid PropertyId,
    Guid InventoryUnitId,
    DateOnly Arrival,
    DateOnly Departure,
    string Reason,
    ManualInventoryBlockStatus Status,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReleasedAtUtc);
