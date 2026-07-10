namespace Reservations.Domain.Events;

using Gma.Framework.Domain;

public sealed record ReservationCreatedDomainEvent : ScopedDomainEvent
{
    public ReservationCreatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid reservationId,
        Guid propertyId,
        Guid allocationRequestId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        long reservationVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.AllocationRequestId = DomainEventGuards.RequireId(allocationRequestId, nameof(allocationRequestId));
        this.Arrival = arrival;
        this.Departure = departure;
        this.InventoryUnitIds = inventoryUnitIds.ToArray();
        this.ReservationVersion = reservationVersion;
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public Guid AllocationRequestId { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public IReadOnlyCollection<Guid> InventoryUnitIds { get; }
    public long ReservationVersion { get; }
}
