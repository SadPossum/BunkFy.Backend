namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;

public sealed record ReservationGuestRecordLinkReadyDomainEvent : ScopedDomainEvent
{
    public ReservationGuestRecordLinkReadyDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid operationId,
        Guid propertyId,
        Guid reservationId,
        int dispatchRevision)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.OperationId = DomainEventGuards.RequireId(operationId, nameof(operationId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.DispatchRevision = dispatchRevision > 0
            ? dispatchRevision
            : throw new ArgumentOutOfRangeException(nameof(dispatchRevision));
    }

    public Guid OperationId { get; }
    public Guid PropertyId { get; }
    public Guid ReservationId { get; }
    public int DispatchRevision { get; }
}
