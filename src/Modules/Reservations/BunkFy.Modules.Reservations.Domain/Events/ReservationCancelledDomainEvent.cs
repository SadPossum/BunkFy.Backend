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
        long reservationVersion,
        string? actorId = null)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.ReservationVersion = reservationVersion;
        this.ActorId = string.IsNullOrWhiteSpace(actorId) ? null : actorId.Trim();
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public long ReservationVersion { get; }
    public string? ActorId { get; }
}
