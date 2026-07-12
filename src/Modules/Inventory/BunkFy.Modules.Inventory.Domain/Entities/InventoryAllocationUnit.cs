namespace BunkFy.Modules.Inventory.Domain.Entities;

using Gma.Framework.Domain.Models;

public sealed class InventoryAllocationUnit : ScopedEntity<Guid>
{
    private InventoryAllocationUnit() { }

    internal InventoryAllocationUnit(Guid inventoryUnitId, string scopeId, Guid allocationId)
        : base(inventoryUnitId, scopeId)
        => this.AllocationId = allocationId;

    public Guid AllocationId { get; private set; }
    public Guid InventoryUnitId => this.Id;
}
