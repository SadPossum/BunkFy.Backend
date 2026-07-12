namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;

public sealed record ReservationCancelledDomainEvent : ScopedDomainEvent
{
    public ReservationCancelledDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid reservationId,
        Guid propertyId,
        long reservationVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.ReservationVersion = reservationVersion;
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public long ReservationVersion { get; }
}
