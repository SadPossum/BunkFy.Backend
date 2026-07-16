namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record BedRetirementFinalizationRejectedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "bed-retirement-finalization-rejected";
    public const int EventVersion = 1;

    public BedRetirementFinalizationRejectedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid topologyChangeId,
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        BedRetirementFinalizationRejectionReason reason)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.TopologyChangeId = IntegrationEventContractGuards.RequireId(topologyChangeId, nameof(topologyChangeId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.BedId = IntegrationEventContractGuards.RequireId(bedId, nameof(bedId));
        this.Reason = reason is BedRetirementFinalizationRejectionReason.RoomNotFound or
            BedRetirementFinalizationRejectionReason.BedNotFound or
            BedRetirementFinalizationRejectionReason.RoomRetired
            ? reason
            : throw new ArgumentOutOfRangeException(nameof(reason));
    }

    public Guid TopologyChangeId { get; }
    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
    public BedRetirementFinalizationRejectionReason Reason { get; }
}
