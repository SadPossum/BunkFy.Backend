namespace Properties.Domain.Events;

using Properties.Domain.Aggregates;
using Properties.Domain.Entities;
using Gma.Framework.Domain;

public sealed record BedUpdatedDomainEvent : TenantDomainEvent
{
    public BedUpdatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        string tenantId,
        string label,
        BedState status)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = DomainEventGuards.RequireId(roomId, nameof(roomId));
        this.BedId = DomainEventGuards.RequireId(bedId, nameof(bedId));
        this.Label = DomainEventGuards.NormalizeRequiredText(label, Room.BedLabelMaxLength, nameof(label));
        this.Status = status;
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
    public string Label { get; }
    public BedState Status { get; }
}
