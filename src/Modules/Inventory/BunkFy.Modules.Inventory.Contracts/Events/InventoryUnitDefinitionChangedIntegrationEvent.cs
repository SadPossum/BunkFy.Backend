namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record InventoryUnitDefinitionChangedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "inventory-unit-definition-changed";
    public const int EventVersion = 1;

    public InventoryUnitDefinitionChangedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid inventoryUnitId,
        Guid propertyId,
        Guid roomId,
        Guid? bedId,
        InventoryUnitKind kind,
        string label,
        bool isTopologyActive,
        bool isSellable,
        long configurationVersion,
        long unitVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.InventoryUnitId = IntegrationEventContractGuards.RequireId(inventoryUnitId, nameof(inventoryUnitId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.BedId = bedId is null ? null : IntegrationEventContractGuards.RequireId(bedId.Value, nameof(bedId));
        this.Kind = kind is InventoryUnitKind.Room or InventoryUnitKind.Bed
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(kind));
        this.Label = string.IsNullOrWhiteSpace(label)
            ? throw new ArgumentException("Inventory unit label is required.", nameof(label))
            : label.Trim();
        this.IsTopologyActive = isTopologyActive;
        this.IsSellable = isSellable;
        this.ConfigurationVersion = configurationVersion > 0
            ? configurationVersion
            : throw new ArgumentOutOfRangeException(nameof(configurationVersion));
        this.UnitVersion = unitVersion > 0
            ? unitVersion
            : throw new ArgumentOutOfRangeException(nameof(unitVersion));
    }

    public Guid InventoryUnitId { get; }
    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid? BedId { get; }
    public InventoryUnitKind Kind { get; }
    public string Label { get; }
    public bool IsTopologyActive { get; }
    public bool IsSellable { get; }
    public long ConfigurationVersion { get; }
    public long UnitVersion { get; }
}
