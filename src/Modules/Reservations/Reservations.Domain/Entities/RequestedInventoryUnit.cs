namespace Reservations.Domain.Entities;

using Gma.Framework.Domain.Models;

public sealed class RequestedInventoryUnit : ScopedEntity<Guid>
{
    private RequestedInventoryUnit() { }

    internal RequestedInventoryUnit(Guid inventoryUnitId, string scopeId, Guid reservationId)
        : base(inventoryUnitId, scopeId)
        => this.ReservationId = reservationId;

    public Guid ReservationId { get; private set; }
    public Guid InventoryUnitId => this.Id;
}
