namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record RoomRetirementFinalizationRejectedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "room-retirement-finalization-rejected";
    public const int EventVersion = 1;

    public RoomRetirementFinalizationRejectedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid topologyChangeId,
        Guid propertyId,
        Guid roomId,
        RoomRetirementFinalizationRejectionReason reason)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.TopologyChangeId = IntegrationEventContractGuards.RequireId(topologyChangeId, nameof(topologyChangeId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.Reason = reason == RoomRetirementFinalizationRejectionReason.RoomNotFound
            ? reason
            : throw new ArgumentOutOfRangeException(nameof(reason));
    }

    public Guid TopologyChangeId { get; }
    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public RoomRetirementFinalizationRejectionReason Reason { get; }
}
