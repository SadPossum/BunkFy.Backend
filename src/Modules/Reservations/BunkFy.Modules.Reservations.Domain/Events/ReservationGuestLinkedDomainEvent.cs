namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Reservations.Domain.Aggregates;

public sealed record ReservationGuestLinkedDomainEvent : ScopedDomainEvent
{
    public ReservationGuestLinkedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid reservationId,
        Guid propertyId,
        Guid guestId,
        ReservationGuestRole role,
        DateOnly arrival,
        DateOnly departure,
        ReservationState status,
        DateOnly? checkedInBusinessDate,
        DateOnly? noShowBusinessDate,
        DateOnly? checkedOutBusinessDate,
        long reservationVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.GuestId = DomainEventGuards.RequireId(guestId, nameof(guestId));
        this.Role = role is ReservationGuestRole.Primary ? role : throw new ArgumentOutOfRangeException(nameof(role));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(arrival, departure);
        this.Arrival = arrival;
        this.Departure = departure;
        this.Status = Enum.IsDefined(status) ? status : throw new ArgumentOutOfRangeException(nameof(status));
        this.CheckedInBusinessDate = checkedInBusinessDate;
        this.NoShowBusinessDate = noShowBusinessDate;
        this.CheckedOutBusinessDate = checkedOutBusinessDate;
        this.ReservationVersion = reservationVersion > 0 ? reservationVersion : throw new ArgumentOutOfRangeException(nameof(reservationVersion));
    }

    public Guid ReservationId { get; }
    public Guid PropertyId { get; }
    public Guid GuestId { get; }
    public ReservationGuestRole Role { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public ReservationState Status { get; }
    public DateOnly? CheckedInBusinessDate { get; }
    public DateOnly? NoShowBusinessDate { get; }
    public DateOnly? CheckedOutBusinessDate { get; }
    public long ReservationVersion { get; }
}
