namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record BedRetirementFinalizedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "bed-retirement-finalized";
    public const int EventVersion = 1;

    public BedRetirementFinalizedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid topologyChangeId,
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        long roomVersion,
        long bedVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.TopologyChangeId = IntegrationEventContractGuards.RequireId(topologyChangeId, nameof(topologyChangeId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.BedId = IntegrationEventContractGuards.RequireId(bedId, nameof(bedId));
        this.RoomVersion = PropertiesEventContractGuards.RequireVersion(roomVersion, nameof(roomVersion));
        this.BedVersion = PropertiesEventContractGuards.RequireVersion(bedVersion, nameof(bedVersion));
    }

    public Guid TopologyChangeId { get; }
    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
    public long RoomVersion { get; }
    public long BedVersion { get; }
}
