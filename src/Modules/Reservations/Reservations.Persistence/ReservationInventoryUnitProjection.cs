namespace Reservations.Persistence;

using Gma.Framework.Domain.Models;
using Inventory.Contracts;
using Reservations.Application.Ports;

public sealed class ReservationInventoryUnitProjection : ScopedEntity<Guid>
{
    private ReservationInventoryUnitProjection() { }

    private ReservationInventoryUnitProjection(ReservationInventoryUnitWriteModel unit)
        : base(unit.InventoryUnitId, unit.ScopeId) => this.ApplyCurrent(unit);

    public Guid PropertyId { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid? BedId { get; private set; }
    public InventoryUnitKind Kind { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public bool IsTopologyActive { get; private set; }
    public bool IsSellable { get; private set; }
    public long ConfigurationVersion { get; private set; }
    public long UnitVersion { get; private set; }

    public static ReservationInventoryUnitProjection Create(ReservationInventoryUnitWriteModel unit) => new(unit);

    public void Apply(ReservationInventoryUnitWriteModel unit)
    {
        if (unit.UnitVersion <= this.UnitVersion)
        {
            return;
        }

        this.ApplyCurrent(unit);
    }

    private void ApplyCurrent(ReservationInventoryUnitWriteModel unit)
    {
        if (this.Id != Guid.Empty && (this.Id != unit.InventoryUnitId || this.ScopeId != unit.ScopeId))
        {
            throw new InvalidOperationException("An inventory unit projection identity cannot change.");
        }

        this.PropertyId = unit.PropertyId;
        this.RoomId = unit.RoomId;
        this.BedId = unit.BedId;
        this.Kind = unit.Kind;
        this.Label = unit.Label;
        this.IsTopologyActive = unit.IsTopologyActive;
        this.IsSellable = unit.IsSellable;
        this.ConfigurationVersion = unit.ConfigurationVersion;
        this.UnitVersion = unit.UnitVersion;
    }
}
