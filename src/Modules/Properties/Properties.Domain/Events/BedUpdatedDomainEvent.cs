namespace Properties.Domain.Events;

using Properties.Domain.Aggregates;
using Properties.Domain.Entities;
using Gma.Framework.Domain;

public sealed record BedUpdatedDomainEvent : ScopedDomainEvent
{
    public BedUpdatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        string tenantId,
        string label,
        BedState status,
        long roomVersion,
        long bedVersion)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = DomainEventGuards.RequireId(roomId, nameof(roomId));
        this.BedId = DomainEventGuards.RequireId(bedId, nameof(bedId));
        this.Label = DomainEventGuards.NormalizeRequiredText(label, Room.BedLabelMaxLength, nameof(label));
        this.Status = status;
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
    public string Label { get; }
    public BedState Status { get; }
    public long RoomVersion { get; }
    public long BedVersion { get; }
}
