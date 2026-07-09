namespace Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record PropertyCreatedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "property-created";
    public const int EventVersion = 1;

    public PropertyCreatedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        string name,
        string code,
        string timeZoneId,
        PropertyStatus status)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Name = IntegrationEventContractGuards.NormalizeRequiredText(name, PropertiesContractLimits.PropertyNameMaxLength, nameof(name));
        this.Code = IntegrationEventContractGuards.NormalizeRequiredText(code, PropertiesContractLimits.PropertyCodeMaxLength, nameof(code));
        this.TimeZoneId = IntegrationEventContractGuards.NormalizeRequiredText(timeZoneId, PropertiesContractLimits.TimeZoneIdMaxLength, nameof(timeZoneId));
        this.Status = PropertiesEventContractGuards.RequireKnown(status, nameof(status));
    }

    public Guid PropertyId { get; }
    public string Name { get; }
    public string Code { get; }
    public string TimeZoneId { get; }
    public PropertyStatus Status { get; }
}
