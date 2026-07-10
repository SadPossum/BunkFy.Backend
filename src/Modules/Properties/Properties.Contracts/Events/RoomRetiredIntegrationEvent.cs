namespace Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record RoomRetiredIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "room-retired";
    public const int EventVersion = 2;

    public RoomRetiredIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        long roomVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.RoomVersion = PropertiesEventContractGuards.RequireVersion(roomVersion, nameof(roomVersion));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public long RoomVersion { get; }
}
