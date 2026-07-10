namespace Inventory.Contracts;

public sealed record ManualInventoryBlockProjectionExport(
    Guid BlockId,
    DateOnly Arrival,
    DateOnly Departure,
    ManualInventoryBlockStatus Status,
    long Version);
