namespace Properties.Domain.Events;

using Properties.Domain.Aggregates;
using Gma.Framework.Domain;

public sealed record PropertyUpdatedDomainEvent : ScopedDomainEvent
{
    public PropertyUpdatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        string tenantId,
        string name,
        string code,
        string timeZoneId,
        PropertyState status,
        long propertyVersion)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.Name = DomainEventGuards.NormalizeRequiredText(name, Property.PropertyNameMaxLength, nameof(name));
        this.Code = DomainEventGuards.NormalizeRequiredText(code, Property.PropertyCodeMaxLength, nameof(code));
        this.TimeZoneId = DomainEventGuards.NormalizeRequiredText(timeZoneId, Property.TimeZoneIdMaxLength, nameof(timeZoneId));
        this.Status = status;
        this.PropertyVersion = propertyVersion > 0
            ? propertyVersion
            : throw new ArgumentOutOfRangeException(nameof(propertyVersion));
    }

    public Guid PropertyId { get; }
    public string Name { get; }
    public string Code { get; }
    public string TimeZoneId { get; }
    public PropertyState Status { get; }
    public long PropertyVersion { get; }
}
