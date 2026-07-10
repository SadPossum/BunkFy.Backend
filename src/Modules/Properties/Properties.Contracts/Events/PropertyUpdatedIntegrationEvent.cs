namespace Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record PropertyUpdatedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "property-updated";
    public const int EventVersion = 2;

    public PropertyUpdatedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        string name,
        string code,
        string timeZoneId,
        PropertyStatus status,
        long propertyVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Name = IntegrationEventContractGuards.NormalizeRequiredText(name, PropertiesContractLimits.PropertyNameMaxLength, nameof(name));
        this.Code = IntegrationEventContractGuards.NormalizeRequiredText(code, PropertiesContractLimits.PropertyCodeMaxLength, nameof(code));
        this.TimeZoneId = IntegrationEventContractGuards.NormalizeRequiredText(timeZoneId, PropertiesContractLimits.TimeZoneIdMaxLength, nameof(timeZoneId));
        this.Status = PropertiesEventContractGuards.RequireKnown(status, nameof(status));
        this.PropertyVersion = PropertiesEventContractGuards.RequireVersion(propertyVersion, nameof(propertyVersion));
    }

    public Guid PropertyId { get; }
    public string Name { get; }
    public string Code { get; }
    public string TimeZoneId { get; }
    public PropertyStatus Status { get; }
    public long PropertyVersion { get; }
}
