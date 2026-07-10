namespace Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ManualInventoryBlockReleasedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "manual-inventory-block-released";
    public const int EventVersion = 1;

    public ManualInventoryBlockReleasedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid blockId,
        Guid propertyId,
        Guid inventoryUnitId,
        long blockVersion)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.BlockId = IntegrationEventContractGuards.RequireId(blockId, nameof(blockId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.InventoryUnitId = IntegrationEventContractGuards.RequireId(inventoryUnitId, nameof(inventoryUnitId));
        this.BlockVersion = blockVersion > 0
            ? blockVersion
            : throw new ArgumentOutOfRangeException(nameof(blockVersion));
    }

    public Guid BlockId { get; }
    public Guid PropertyId { get; }
    public Guid InventoryUnitId { get; }
    public long BlockVersion { get; }
}
