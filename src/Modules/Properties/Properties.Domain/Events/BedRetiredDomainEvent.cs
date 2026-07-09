namespace Properties.Domain.Events;

using Gma.Framework.Domain;

public sealed record BedRetiredDomainEvent : TenantDomainEvent
{
    public BedRetiredDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        string tenantId)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = DomainEventGuards.RequireId(roomId, nameof(roomId));
        this.BedId = DomainEventGuards.RequireId(bedId, nameof(bedId));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
}
