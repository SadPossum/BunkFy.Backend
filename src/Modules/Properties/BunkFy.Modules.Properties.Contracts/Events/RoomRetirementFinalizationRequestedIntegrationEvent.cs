namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record RoomRetirementFinalizationRequestedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "room-retirement-finalization-requested";
    public const int EventVersion = 1;

    public RoomRetirementFinalizationRequestedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid topologyChangeId,
        Guid propertyId,
        Guid roomId)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.TopologyChangeId = IntegrationEventContractGuards.RequireId(topologyChangeId, nameof(topologyChangeId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
    }

    public Guid TopologyChangeId { get; }
    public Guid PropertyId { get; }
    public Guid RoomId { get; }
}
