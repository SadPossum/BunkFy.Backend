namespace Properties.Domain.Events;

using Gma.Framework.Domain;

public sealed record RoomRetiredDomainEvent : ScopedDomainEvent
{
    public RoomRetiredDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        string tenantId,
        long roomVersion)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = DomainEventGuards.RequireId(roomId, nameof(roomId));
        this.RoomVersion = roomVersion > 0
            ? roomVersion
            : throw new ArgumentOutOfRangeException(nameof(roomVersion));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public long RoomVersion { get; }
}
