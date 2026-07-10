namespace Properties.Domain.Events;

using Gma.Framework.Domain;

public sealed record BedRetiredDomainEvent : ScopedDomainEvent
{
    public BedRetiredDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        string tenantId,
        long roomVersion,
        long bedVersion)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = DomainEventGuards.RequireId(roomId, nameof(roomId));
        this.BedId = DomainEventGuards.RequireId(bedId, nameof(bedId));
        this.RoomVersion = roomVersion > 0
            ? roomVersion
            : throw new ArgumentOutOfRangeException(nameof(roomVersion));
        this.BedVersion = bedVersion > 0
            ? bedVersion
            : throw new ArgumentOutOfRangeException(nameof(bedVersion));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
    public long RoomVersion { get; }
    public long BedVersion { get; }
}
