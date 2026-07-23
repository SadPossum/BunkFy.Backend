namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record PropertyProcessingPolicyActivatedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "property-processing-policy-activated";
    public const int EventVersion = 1;

    public PropertyProcessingPolicyActivatedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        PropertyGovernancePolicyBinding binding,
        long propertyVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        this.PropertyVersion = PropertiesEventContractGuards.RequireVersion(propertyVersion, nameof(propertyVersion));
    }

    public Guid PropertyId { get; }
    public PropertyGovernancePolicyBinding Binding { get; }
    public long PropertyVersion { get; }
}
