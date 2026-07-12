namespace BunkFy.Modules.Reservations.Persistence;

using Gma.Framework.Domain.Models;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;

public sealed class ReservationInventoryAllocationProjection : ScopedEntity<Guid>
{
    private readonly List<ReservationInventoryAllocationUnitProjection> units = [];

    private ReservationInventoryAllocationProjection() { }

    private ReservationInventoryAllocationProjection(string scopeId, Guid allocationId)
        : base(allocationId, scopeId) { }

    public Guid ReservationId { get; private set; }
    public Guid? PropertyId { get; private set; }
    public DateOnly? Arrival { get; private set; }
    public DateOnly? Departure { get; private set; }
    public InventoryAllocationStatus Status { get; private set; } = InventoryAllocationStatus.Released;
    public long Version { get; private set; }
    public bool IsKnown { get; private set; }
    public IReadOnlyCollection<ReservationInventoryAllocationUnitProjection> Units => this.units.AsReadOnly();

    public static ReservationInventoryAllocationProjection Create(ReservationInventoryAllocationWriteModel allocation)
    {
        ReservationInventoryAllocationProjection projection = new(allocation.ScopeId, allocation.AllocationId);
        projection.Apply(allocation);
        return projection;
    }

    public static ReservationInventoryAllocationProjection CreateReleasedTombstone(
        string scopeId,
        Guid allocationId,
        Guid reservationId,
        long version) =>
        new(scopeId, allocationId)
        {
            ReservationId = reservationId,
            Status = InventoryAllocationStatus.Released,
            Version = version
        };

    public void Apply(ReservationInventoryAllocationWriteModel allocation)
    {
        if (allocation.Version < this.Version || (allocation.Version == this.Version && this.IsKnown))
        {
            return;
        }

        this.ReservationId = allocation.ReservationId;
        this.PropertyId = allocation.PropertyId;
        this.Arrival = allocation.Arrival;
        this.Departure = allocation.Departure;
        this.Status = allocation.Status;
        this.Version = allocation.Version;
        this.IsKnown = true;
        this.units.Clear();
        this.units.AddRange(allocation.InventoryUnitIds
            .Distinct()
            .Select(unitId => new ReservationInventoryAllocationUnitProjection(unitId, this.ScopeId, this.Id)));
    }

    public void Release(Guid reservationId, long version)
    {
        if (version < this.Version)
        {
            return;
        }

        this.ReservationId = reservationId;
        this.Status = InventoryAllocationStatus.Released;
        this.Version = version;
    }
}

public sealed class ReservationInventoryAllocationUnitProjection : ScopedEntity<Guid>
{
    private ReservationInventoryAllocationUnitProjection() { }

    internal ReservationInventoryAllocationUnitProjection(Guid inventoryUnitId, string scopeId, Guid allocationId)
        : base(inventoryUnitId, scopeId) => this.AllocationId = allocationId;

    public Guid AllocationId { get; private set; }
    public Guid InventoryUnitId => this.Id;
}
