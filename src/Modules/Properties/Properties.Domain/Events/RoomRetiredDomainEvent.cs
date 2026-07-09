namespace Properties.Domain.Events;

using Gma.Framework.Domain;

public sealed record RoomRetiredDomainEvent : TenantDomainEvent
{
    public RoomRetiredDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        string tenantId)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = DomainEventGuards.RequireId(roomId, nameof(roomId));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
}
