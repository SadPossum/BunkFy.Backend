namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;

public sealed record ReservationAnonymisedDomainEvent : ScopedDomainEvent
{
    public ReservationAnonymisedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid receiptId,
        Guid propertyId,
        Guid reservationId,
        long reservationVersion,
        long detailsRevision)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReceiptId = receiptId;
        this.PropertyId = propertyId;
        this.ReservationId = reservationId;
        this.ReservationVersion = reservationVersion;
        this.DetailsRevision = detailsRevision;
    }

    public Guid ReceiptId { get; }
    public Guid PropertyId { get; }
    public Guid ReservationId { get; }
    public long ReservationVersion { get; }
    public long DetailsRevision { get; }
}
