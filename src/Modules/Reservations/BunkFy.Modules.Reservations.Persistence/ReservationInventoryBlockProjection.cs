namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Domain.Models;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;

public sealed class ReservationInventoryBlockProjection : ScopedEntity<Guid>
{
    private ReservationInventoryBlockProjection() { }

    private ReservationInventoryBlockProjection(string scopeId, Guid blockId)
        : base(blockId, scopeId) { }

    public Guid PropertyId { get; private set; }
    public Guid InventoryUnitId { get; private set; }
    public DateOnly? Arrival { get; private set; }
    public DateOnly? Departure { get; private set; }
    public ManualInventoryBlockStatus Status { get; private set; } = ManualInventoryBlockStatus.Released;
    public long Version { get; private set; }
    public bool IsKnown { get; private set; }

    public static ReservationInventoryBlockProjection Create(ReservationInventoryBlockWriteModel block)
    {
        ReservationInventoryBlockProjection projection = new(block.ScopeId, block.BlockId);
        projection.Apply(block);
        return projection;
    }

    public static ReservationInventoryBlockProjection CreateReleasedTombstone(
        string scopeId,
        Guid blockId,
        Guid propertyId,
        Guid inventoryUnitId,
        long version) =>
        new(scopeId, blockId)
        {
            PropertyId = propertyId,
            InventoryUnitId = inventoryUnitId,
            Status = ManualInventoryBlockStatus.Released,
            Version = version
        };

    public void Apply(ReservationInventoryBlockWriteModel block)
    {
        if (block.Version < this.Version || (block.Version == this.Version && this.IsKnown))
        {
            return;
        }

        this.PropertyId = block.PropertyId;
        this.InventoryUnitId = block.InventoryUnitId;
        this.Arrival = block.Arrival;
        this.Departure = block.Departure;
        this.Status = block.Status;
        this.Version = block.Version;
        this.IsKnown = true;
    }

    public void Release(Guid propertyId, Guid inventoryUnitId, long version)
    {
        if (version < this.Version)
        {
            return;
        }

        this.PropertyId = propertyId;
        this.InventoryUnitId = inventoryUnitId;
        this.Status = ManualInventoryBlockStatus.Released;
        this.Version = version;
    }
}
