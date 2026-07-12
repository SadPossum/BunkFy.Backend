namespace BunkFy.Modules.Inventory.Contracts;

public sealed record InventoryAllocationProjectionExport(
    Guid AllocationId,
    Guid ReservationId,
    DateOnly Arrival,
    DateOnly Departure,
    InventoryAllocationStatus Status,
    long Version);
