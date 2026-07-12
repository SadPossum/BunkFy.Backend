namespace BunkFy.Modules.Properties.Domain.Events;

using Gma.Framework.Domain;

public sealed record PropertyRetiredDomainEvent : ScopedDomainEvent
{
    public PropertyRetiredDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        string tenantId,
        long propertyVersion)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.PropertyVersion = propertyVersion > 0
            ? propertyVersion
            : throw new ArgumentOutOfRangeException(nameof(propertyVersion));
    }

    public Guid PropertyId { get; }
    public long PropertyVersion { get; }
}
