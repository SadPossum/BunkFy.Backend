namespace BunkFy.Modules.Properties.Domain.Events;

using BunkFy.Modules.Properties.Domain.Aggregates;
using Gma.Framework.Domain;

public sealed record PropertyRetiredDomainEvent : ScopedDomainEvent
{
    public PropertyRetiredDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        string tenantId,
        long propertyVersion,
        string? actorId = null)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.PropertyVersion = propertyVersion > 0
            ? propertyVersion
            : throw new ArgumentOutOfRangeException(nameof(propertyVersion));
        this.ActorId = string.IsNullOrWhiteSpace(actorId)
            ? null
            : DomainEventGuards.NormalizeRequiredText(actorId, Property.ActorIdMaxLength, nameof(actorId));
    }

    public Guid PropertyId { get; }
    public long PropertyVersion { get; }
    public string? ActorId { get; }
}
