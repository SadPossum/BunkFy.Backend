namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;

public sealed record ReservationNoShowRequestedDomainEvent : ScopedDomainEvent
{
    public ReservationNoShowRequestedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid reservationId,
        Guid propertyId,
        Guid allocationId,
        Guid releaseRequestId,
        long expectedAllocationVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.AllocationId = DomainEventGuards.RequireId(allocationId, nameof(allocationId));
        this.ReleaseRequestId = DomainEventGuards.RequireId(releaseRequestId, nameof(releaseRequestId));
        this.ExpectedAllocationVersion = expectedAllocationVersion > 0
            ? expectedAllocationVersion
            : throw new ArgumentOutOfRangeException(nameof(expectedAllocationVersion));
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public Guid AllocationId { get; }
    public Guid ReleaseRequestId { get; }
    public long ExpectedAllocationVersion { get; }
}
