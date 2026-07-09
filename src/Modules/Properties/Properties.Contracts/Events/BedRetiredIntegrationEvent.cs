namespace Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record BedRetiredIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "bed-retired";
    public const int EventVersion = 1;

    public BedRetiredIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        Guid bedId)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.BedId = IntegrationEventContractGuards.RequireId(bedId, nameof(bedId));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
}
