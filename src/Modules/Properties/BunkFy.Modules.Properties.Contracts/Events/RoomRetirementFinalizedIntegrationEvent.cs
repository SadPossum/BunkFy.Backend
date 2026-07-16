namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record RoomRetirementFinalizedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "room-retirement-finalized";
    public const int EventVersion = 1;

    public RoomRetirementFinalizedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid topologyChangeId,
        Guid propertyId,
        Guid roomId,
        long roomVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.TopologyChangeId = IntegrationEventContractGuards.RequireId(topologyChangeId, nameof(topologyChangeId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.RoomVersion = PropertiesEventContractGuards.RequireVersion(roomVersion, nameof(roomVersion));
    }

    public Guid TopologyChangeId { get; }
    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public long RoomVersion { get; }
}
