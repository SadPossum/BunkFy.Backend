namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;

public sealed record ReservationAllocationAmendmentRequestedDomainEvent : ScopedDomainEvent
{
    public ReservationAllocationAmendmentRequestedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid reservationId,
        Guid propertyId,
        Guid amendmentRequestId,
        Guid allocationId,
        long expectedAllocationVersion,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.AmendmentRequestId = DomainEventGuards.RequireId(amendmentRequestId, nameof(amendmentRequestId));
        this.AllocationId = DomainEventGuards.RequireId(allocationId, nameof(allocationId));
        this.ExpectedAllocationVersion = expectedAllocationVersion;
        this.Arrival = arrival;
        this.Departure = departure;
        this.InventoryUnitIds = inventoryUnitIds.ToArray();
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public Guid AmendmentRequestId { get; }
    public Guid AllocationId { get; }
    public long ExpectedAllocationVersion { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public IReadOnlyCollection<Guid> InventoryUnitIds { get; }
}
