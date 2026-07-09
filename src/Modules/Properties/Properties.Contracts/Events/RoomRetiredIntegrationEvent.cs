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
    public const int EventVersion = 1;

    public RoomRetiredIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
}
