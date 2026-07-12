namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record PropertyRetiredIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "property-retired";
    public const int EventVersion = 1;

    public PropertyRetiredIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        long propertyVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.PropertyVersion = PropertiesEventContractGuards.RequireVersion(propertyVersion, nameof(propertyVersion));
    }

    public Guid PropertyId { get; }
    public long PropertyVersion { get; }
}
