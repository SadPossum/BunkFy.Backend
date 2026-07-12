namespace BunkFy.Modules.Inventory.Contracts;

public sealed record InventoryUnitProjectionExport(
    Guid InventoryUnitId,
    Guid RoomId,
    Guid? BedId,
    InventoryUnitKind Kind,
    string Label,
    bool IsTopologyActive,
    bool IsSellable,
    long ConfigurationVersion,
    long UnitVersion,
    IReadOnlyCollection<ManualInventoryBlockProjectionExport> Blocks,
    IReadOnlyCollection<InventoryAllocationProjectionExport> Allocations);
