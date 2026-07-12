namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Reservations.Domain.Aggregates;

public sealed record ReservationCheckedInDomainEvent : ScopedDomainEvent
{
    public ReservationCheckedInDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid reservationId,
        Guid propertyId,
        DateOnly businessDate,
        string actorId,
        long reservationVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.BusinessDate = businessDate;
        this.ActorId = RequireActor(actorId);
        this.ReservationVersion = reservationVersion > 0
            ? reservationVersion
            : throw new ArgumentOutOfRangeException(nameof(reservationVersion));
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public DateOnly BusinessDate { get; }
    public string ActorId { get; }
    public long ReservationVersion { get; }

    private static string RequireActor(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= Reservation.ActorIdMaxLength
            ? normalized
            : throw new ArgumentException("Actor id is invalid.", nameof(value));
    }
}
