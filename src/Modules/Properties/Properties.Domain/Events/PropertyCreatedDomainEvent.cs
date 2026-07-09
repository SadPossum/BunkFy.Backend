namespace Properties.Domain.Events;

using Properties.Domain.Aggregates;
using Gma.Framework.Domain;

public sealed record PropertyCreatedDomainEvent : TenantDomainEvent
{
    public PropertyCreatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        string tenantId,
        string name,
        string code,
        string timeZoneId,
        PropertyState status)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.Name = DomainEventGuards.NormalizeRequiredText(name, Property.PropertyNameMaxLength, nameof(name));
        this.Code = DomainEventGuards.NormalizeRequiredText(code, Property.PropertyCodeMaxLength, nameof(code));
        this.TimeZoneId = DomainEventGuards.NormalizeRequiredText(timeZoneId, Property.TimeZoneIdMaxLength, nameof(timeZoneId));
        this.Status = status;
    }

    public Guid PropertyId { get; }
    public string Name { get; }
    public string Code { get; }
    public string TimeZoneId { get; }
    public PropertyState Status { get; }
}
