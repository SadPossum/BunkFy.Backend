namespace Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record RoomCreatedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "room-created";
    public const int EventVersion = 1;

    public RoomCreatedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        string name,
        string? buildingLabel,
        string? floorLabel,
        RoomStatus status)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.Name = IntegrationEventContractGuards.NormalizeRequiredText(name, PropertiesContractLimits.RoomNameMaxLength, nameof(name));
        this.BuildingLabel = PropertiesEventContractGuards.NormalizeOptionalLabel(buildingLabel, nameof(buildingLabel));
        this.FloorLabel = PropertiesEventContractGuards.NormalizeOptionalLabel(floorLabel, nameof(floorLabel));
        this.Status = PropertiesEventContractGuards.RequireKnown(status, nameof(status));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public string Name { get; }
    public string? BuildingLabel { get; }
    public string? FloorLabel { get; }
    public RoomStatus Status { get; }
}
