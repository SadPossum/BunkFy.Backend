namespace BunkFy.Modules.Inventory.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ManualInventoryBlockCreatedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "manual-inventory-block-created";
    public const int EventVersion = 2;

    public ManualInventoryBlockCreatedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid blockId,
        Guid blockGroupId,
        Guid propertyId,
        Guid inventoryUnitId,
        DateOnly arrival,
        DateOnly departure,
        string reason,
        long blockVersion,
        string? actorId = null)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.BlockId = IntegrationEventContractGuards.RequireId(blockId, nameof(blockId));
        this.BlockGroupId = IntegrationEventContractGuards.RequireId(blockGroupId, nameof(blockGroupId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.InventoryUnitId = IntegrationEventContractGuards.RequireId(inventoryUnitId, nameof(inventoryUnitId));
        this.Arrival = arrival;
        this.Departure = departure > arrival
            ? departure
            : throw new ArgumentOutOfRangeException(nameof(departure));
        this.Reason = string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Block reason is required.", nameof(reason))
            : reason.Trim();
        this.BlockVersion = blockVersion > 0
            ? blockVersion
            : throw new ArgumentOutOfRangeException(nameof(blockVersion));
        this.ActorId = OptionalActor(actorId);
    }

    public Guid BlockId { get; }
    public Guid BlockGroupId { get; }
    public Guid PropertyId { get; }
    public Guid InventoryUnitId { get; }
    public DateOnly Arrival { get; }
    public DateOnly Departure { get; }
    public string Reason { get; }
    public long BlockVersion { get; }
    public string? ActorId { get; }

    private static string? OptionalActor(string? value)
    {
        string? normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null || normalized.Length <= 200
            ? normalized
            : throw new ArgumentException("Actor id is invalid.", nameof(value));
    }
}
