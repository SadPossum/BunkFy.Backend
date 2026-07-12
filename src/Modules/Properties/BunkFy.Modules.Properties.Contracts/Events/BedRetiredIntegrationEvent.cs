namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record BedRetiredIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "bed-retired";
    public const int EventVersion = 2;

    public BedRetiredIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        long roomVersion,
        long bedVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.BedId = IntegrationEventContractGuards.RequireId(bedId, nameof(bedId));
        this.RoomVersion = PropertiesEventContractGuards.RequireVersion(roomVersion, nameof(roomVersion));
        this.BedVersion = PropertiesEventContractGuards.RequireVersion(bedVersion, nameof(bedVersion));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
    public long RoomVersion { get; }
    public long BedVersion { get; }
}
