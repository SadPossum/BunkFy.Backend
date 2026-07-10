namespace Inventory.Application.Ports;

public sealed record InventoryAllocationUnitSnapshot(
    Guid InventoryUnitId,
    bool IsTopologyActive,
    bool IsSellable);
