namespace BunkFy.Modules.Inventory.Persistence;

using Gma.Framework.Domain.Models;

internal sealed class InventoryAllocationOperationLock
    : ScopedEntity<Guid>
{
    private InventoryAllocationOperationLock() { }

    public InventoryAllocationOperationLock(
        Guid id,
        string scopeId,
        Guid allocationId)
        : base(id, scopeId)
    {
        if (id == Guid.Empty || allocationId == Guid.Empty)
        {
            throw new ArgumentException(
                "The inventory allocation operation-lock coordinate is invalid.");
        }

        this.AllocationId = allocationId;
    }

    public Guid AllocationId { get; private set; }
    public long Revision { get; private set; } = 1;

    public void Touch() =>
        this.Revision = checked(this.Revision + 1);
}
