namespace Inventory.Contracts;

public sealed record InventoryAvailabilityProjectionExport(
    string TenantId,
    Guid PropertyId,
    IReadOnlyCollection<InventoryUnitProjectionExport> Units);
