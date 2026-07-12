namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record RoomSalesModeChangedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "room-sales-mode-changed";
    public const int EventVersion = 1;

    public RoomSalesModeChangedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        Guid roomId,
        InventorySalesMode salesMode,
        long configurationVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.SalesMode = salesMode is InventorySalesMode.RoomLevel or InventorySalesMode.BedLevel
            ? salesMode
            : throw new ArgumentOutOfRangeException(nameof(salesMode));
        this.ConfigurationVersion = configurationVersion > 0
            ? configurationVersion
            : throw new ArgumentOutOfRangeException(nameof(configurationVersion));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public InventorySalesMode SalesMode { get; }
    public long ConfigurationVersion { get; }
}
